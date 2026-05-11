#include "gemap/gemap.h"

#include <ctype.h>
#include <errno.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef enum json_type
{
    JSON_NULL,
    JSON_BOOL,
    JSON_NUMBER,
    JSON_STRING,
    JSON_ARRAY,
    JSON_OBJECT
} json_type;

typedef struct json_value json_value;

typedef struct json_object_entry
{
    char* name;
    json_value* value;
} json_object_entry;

struct json_value
{
    json_type type;
    union
    {
        int boolean;
        double number;
        char* string;
        struct
        {
            size_t count;
            json_value** items;
        } array;
        struct
        {
            size_t count;
            json_object_entry* entries;
        } object;
    } as;
};

typedef struct json_parser
{
    const char* text;
    size_t length;
    size_t position;
    gemap_error* error;
} json_parser;

static void set_error(gemap_error* error, gemap_result code, const char* format, ...)
{
    va_list args;

    if (error == NULL)
    {
        return;
    }

    error->code = code;
    va_start(args, format);
    (void)vsnprintf(error->message, sizeof(error->message), format, args);
    va_end(args);
}

static char* duplicate_range(const char* start, size_t length)
{
    char* result = (char*)malloc(length + 1);
    if (result == NULL)
    {
        return NULL;
    }

    if (length > 0)
    {
        memcpy(result, start, length);
    }

    result[length] = '\0';
    return result;
}

static char* duplicate_string(const char* value)
{
    return value == NULL ? NULL : duplicate_range(value, strlen(value));
}

static int reserve_buffer(char** buffer, size_t* capacity, size_t required)
{
    char* grown;
    size_t new_capacity = *capacity == 0 ? 32 : *capacity;

    while (new_capacity < required)
    {
        if (new_capacity > ((size_t)-1) / 2)
        {
            return 0;
        }
        new_capacity *= 2;
    }

    grown = (char*)realloc(*buffer, new_capacity);
    if (grown == NULL)
    {
        return 0;
    }

    *buffer = grown;
    *capacity = new_capacity;
    return 1;
}

static int append_byte(char** buffer, size_t* length, size_t* capacity, char value)
{
    if (!reserve_buffer(buffer, capacity, *length + 2))
    {
        return 0;
    }

    (*buffer)[*length] = value;
    *length += 1;
    (*buffer)[*length] = '\0';
    return 1;
}

static int append_utf8(char** buffer, size_t* length, size_t* capacity, unsigned int codepoint)
{
    if (codepoint <= 0x7F)
    {
        return append_byte(buffer, length, capacity, (char)codepoint);
    }

    if (codepoint <= 0x7FF)
    {
        return append_byte(buffer, length, capacity, (char)(0xC0 | (codepoint >> 6)))
            && append_byte(buffer, length, capacity, (char)(0x80 | (codepoint & 0x3F)));
    }

    if (codepoint <= 0xFFFF)
    {
        return append_byte(buffer, length, capacity, (char)(0xE0 | (codepoint >> 12)))
            && append_byte(buffer, length, capacity, (char)(0x80 | ((codepoint >> 6) & 0x3F)))
            && append_byte(buffer, length, capacity, (char)(0x80 | (codepoint & 0x3F)));
    }

    return append_byte(buffer, length, capacity, (char)(0xF0 | (codepoint >> 18)))
        && append_byte(buffer, length, capacity, (char)(0x80 | ((codepoint >> 12) & 0x3F)))
        && append_byte(buffer, length, capacity, (char)(0x80 | ((codepoint >> 6) & 0x3F)))
        && append_byte(buffer, length, capacity, (char)(0x80 | (codepoint & 0x3F)));
}

static json_value* json_new(json_type type)
{
    json_value* value = (json_value*)calloc(1, sizeof(json_value));
    if (value != NULL)
    {
        value->type = type;
    }

    return value;
}

static void json_free(json_value* value)
{
    size_t i;

    if (value == NULL)
    {
        return;
    }

    switch (value->type)
    {
    case JSON_STRING:
        free(value->as.string);
        break;
    case JSON_ARRAY:
        for (i = 0; i < value->as.array.count; i++)
        {
            json_free(value->as.array.items[i]);
        }
        free(value->as.array.items);
        break;
    case JSON_OBJECT:
        for (i = 0; i < value->as.object.count; i++)
        {
            free(value->as.object.entries[i].name);
            json_free(value->as.object.entries[i].value);
        }
        free(value->as.object.entries);
        break;
    default:
        break;
    }

    free(value);
}

static void skip_ws(json_parser* parser)
{
    while (parser->position < parser->length)
    {
        unsigned char c = (unsigned char)parser->text[parser->position];
        if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
        {
            break;
        }

        parser->position++;
    }
}

static int match_literal(json_parser* parser, const char* literal)
{
    size_t length = strlen(literal);
    if (parser->position + length > parser->length)
    {
        return 0;
    }

    if (memcmp(parser->text + parser->position, literal, length) != 0)
    {
        return 0;
    }

    parser->position += length;
    return 1;
}

static int parse_hex4(json_parser* parser, unsigned int* out_value)
{
    unsigned int value = 0;
    int i;

    if (parser->position + 4 > parser->length)
    {
        return 0;
    }

    for (i = 0; i < 4; i++)
    {
        unsigned char c = (unsigned char)parser->text[parser->position + (size_t)i];
        value <<= 4;
        if (c >= '0' && c <= '9')
        {
            value += (unsigned int)(c - '0');
        }
        else if (c >= 'a' && c <= 'f')
        {
            value += (unsigned int)(c - 'a' + 10);
        }
        else if (c >= 'A' && c <= 'F')
        {
            value += (unsigned int)(c - 'A' + 10);
        }
        else
        {
            return 0;
        }
    }

    parser->position += 4;
    *out_value = value;
    return 1;
}

static char* parse_string_raw(json_parser* parser)
{
    char* buffer = NULL;
    size_t length = 0;
    size_t capacity = 0;

    if (parser->position >= parser->length || parser->text[parser->position] != '"')
    {
        set_error(parser->error, GEMAP_ERROR_PARSE, "Expected string at byte %zu.", parser->position);
        return NULL;
    }

    parser->position++;
    while (parser->position < parser->length)
    {
        unsigned char c = (unsigned char)parser->text[parser->position++];
        if (c == '"')
        {
            if (!reserve_buffer(&buffer, &capacity, length + 1))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }

            buffer[length] = '\0';
            return buffer;
        }

        if (c < 0x20)
        {
            set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid control character in string at byte %zu.", parser->position - 1);
            free(buffer);
            return NULL;
        }

        if (c != '\\')
        {
            if (!append_byte(&buffer, &length, &capacity, (char)c))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            continue;
        }

        if (parser->position >= parser->length)
        {
            set_error(parser->error, GEMAP_ERROR_PARSE, "Unterminated escape sequence.");
            free(buffer);
            return NULL;
        }

        c = (unsigned char)parser->text[parser->position++];
        switch (c)
        {
        case '"':
        case '\\':
        case '/':
            if (!append_byte(&buffer, &length, &capacity, (char)c))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 'b':
            if (!append_byte(&buffer, &length, &capacity, '\b'))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 'f':
            if (!append_byte(&buffer, &length, &capacity, '\f'))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 'n':
            if (!append_byte(&buffer, &length, &capacity, '\n'))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 'r':
            if (!append_byte(&buffer, &length, &capacity, '\r'))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 't':
            if (!append_byte(&buffer, &length, &capacity, '\t'))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        case 'u':
        {
            unsigned int codepoint;
            if (!parse_hex4(parser, &codepoint))
            {
                set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid unicode escape in string.");
                free(buffer);
                return NULL;
            }

            if (codepoint >= 0xD800 && codepoint <= 0xDBFF)
            {
                unsigned int low;
                if (parser->position + 6 <= parser->length
                    && parser->text[parser->position] == '\\'
                    && parser->text[parser->position + 1] == 'u')
                {
                    parser->position += 2;
                    if (!parse_hex4(parser, &low) || low < 0xDC00 || low > 0xDFFF)
                    {
                        set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid unicode surrogate pair.");
                        free(buffer);
                        return NULL;
                    }

                    codepoint = 0x10000 + (((codepoint - 0xD800) << 10) | (low - 0xDC00));
                }
                else
                {
                    set_error(parser->error, GEMAP_ERROR_PARSE, "Missing unicode low surrogate.");
                    free(buffer);
                    return NULL;
                }
            }

            if (!append_utf8(&buffer, &length, &capacity, codepoint))
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
                free(buffer);
                return NULL;
            }
            break;
        }
        default:
            set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid escape sequence in string.");
            free(buffer);
            return NULL;
        }
    }

    set_error(parser->error, GEMAP_ERROR_PARSE, "Unterminated string.");
    free(buffer);
    return NULL;
}

static json_value* parse_value(json_parser* parser);

static int append_array_item(json_value* array, json_value* item)
{
    json_value** grown = (json_value**)realloc(array->as.array.items, (array->as.array.count + 1) * sizeof(json_value*));
    if (grown == NULL)
    {
        return 0;
    }

    array->as.array.items = grown;
    array->as.array.items[array->as.array.count++] = item;
    return 1;
}

static int append_object_entry(json_value* object, char* name, json_value* value)
{
    json_object_entry* grown = (json_object_entry*)realloc(
        object->as.object.entries,
        (object->as.object.count + 1) * sizeof(json_object_entry));
    if (grown == NULL)
    {
        return 0;
    }

    object->as.object.entries = grown;
    object->as.object.entries[object->as.object.count].name = name;
    object->as.object.entries[object->as.object.count].value = value;
    object->as.object.count++;
    return 1;
}

static json_value* parse_array(json_parser* parser)
{
    json_value* array = json_new(JSON_ARRAY);
    if (array == NULL)
    {
        set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing array.");
        return NULL;
    }

    parser->position++;
    skip_ws(parser);
    if (parser->position < parser->length && parser->text[parser->position] == ']')
    {
        parser->position++;
        return array;
    }

    while (parser->position < parser->length)
    {
        json_value* item;
        skip_ws(parser);
        item = parse_value(parser);
        if (item == NULL)
        {
            json_free(array);
            return NULL;
        }

        if (!append_array_item(array, item))
        {
            json_free(item);
            json_free(array);
            set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing array.");
            return NULL;
        }

        skip_ws(parser);
        if (parser->position < parser->length && parser->text[parser->position] == ',')
        {
            parser->position++;
            continue;
        }

        if (parser->position < parser->length && parser->text[parser->position] == ']')
        {
            parser->position++;
            return array;
        }

        set_error(parser->error, GEMAP_ERROR_PARSE, "Expected ',' or ']' at byte %zu.", parser->position);
        json_free(array);
        return NULL;
    }

    set_error(parser->error, GEMAP_ERROR_PARSE, "Unterminated array.");
    json_free(array);
    return NULL;
}

static json_value* parse_object(json_parser* parser)
{
    json_value* object = json_new(JSON_OBJECT);
    if (object == NULL)
    {
        set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing object.");
        return NULL;
    }

    parser->position++;
    skip_ws(parser);
    if (parser->position < parser->length && parser->text[parser->position] == '}')
    {
        parser->position++;
        return object;
    }

    while (parser->position < parser->length)
    {
        char* name;
        json_value* value;

        skip_ws(parser);
        name = parse_string_raw(parser);
        if (name == NULL)
        {
            json_free(object);
            return NULL;
        }

        skip_ws(parser);
        if (parser->position >= parser->length || parser->text[parser->position] != ':')
        {
            free(name);
            json_free(object);
            set_error(parser->error, GEMAP_ERROR_PARSE, "Expected ':' at byte %zu.", parser->position);
            return NULL;
        }

        parser->position++;
        skip_ws(parser);
        value = parse_value(parser);
        if (value == NULL)
        {
            free(name);
            json_free(object);
            return NULL;
        }

        if (!append_object_entry(object, name, value))
        {
            free(name);
            json_free(value);
            json_free(object);
            set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing object.");
            return NULL;
        }

        skip_ws(parser);
        if (parser->position < parser->length && parser->text[parser->position] == ',')
        {
            parser->position++;
            continue;
        }

        if (parser->position < parser->length && parser->text[parser->position] == '}')
        {
            parser->position++;
            return object;
        }

        set_error(parser->error, GEMAP_ERROR_PARSE, "Expected ',' or '}' at byte %zu.", parser->position);
        json_free(object);
        return NULL;
    }

    set_error(parser->error, GEMAP_ERROR_PARSE, "Unterminated object.");
    json_free(object);
    return NULL;
}

static json_value* parse_number(json_parser* parser)
{
    size_t start = parser->position;
    char* raw;
    char* end = NULL;
    json_value* value;

    if (parser->text[parser->position] == '-')
    {
        parser->position++;
    }

    if (parser->position >= parser->length || !isdigit((unsigned char)parser->text[parser->position]))
    {
        set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid number at byte %zu.", start);
        return NULL;
    }

    if (parser->text[parser->position] == '0')
    {
        parser->position++;
    }
    else
    {
        while (parser->position < parser->length && isdigit((unsigned char)parser->text[parser->position]))
        {
            parser->position++;
        }
    }

    if (parser->position < parser->length && parser->text[parser->position] == '.')
    {
        parser->position++;
        if (parser->position >= parser->length || !isdigit((unsigned char)parser->text[parser->position]))
        {
            set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid number at byte %zu.", start);
            return NULL;
        }

        while (parser->position < parser->length && isdigit((unsigned char)parser->text[parser->position]))
        {
            parser->position++;
        }
    }

    if (parser->position < parser->length && (parser->text[parser->position] == 'e' || parser->text[parser->position] == 'E'))
    {
        parser->position++;
        if (parser->position < parser->length && (parser->text[parser->position] == '+' || parser->text[parser->position] == '-'))
        {
            parser->position++;
        }

        if (parser->position >= parser->length || !isdigit((unsigned char)parser->text[parser->position]))
        {
            set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid number at byte %zu.", start);
            return NULL;
        }

        while (parser->position < parser->length && isdigit((unsigned char)parser->text[parser->position]))
        {
            parser->position++;
        }
    }

    raw = duplicate_range(parser->text + start, parser->position - start);
    if (raw == NULL)
    {
        set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing number.");
        return NULL;
    }

    value = json_new(JSON_NUMBER);
    if (value == NULL)
    {
        free(raw);
        set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing number.");
        return NULL;
    }

    errno = 0;
    value->as.number = strtod(raw, &end);
    if (errno != 0 || end == raw || *end != '\0')
    {
        free(raw);
        json_free(value);
        set_error(parser->error, GEMAP_ERROR_PARSE, "Invalid number at byte %zu.", start);
        return NULL;
    }

    free(raw);
    return value;
}

static json_value* parse_value(json_parser* parser)
{
    json_value* value;

    skip_ws(parser);
    if (parser->position >= parser->length)
    {
        set_error(parser->error, GEMAP_ERROR_PARSE, "Unexpected end of JSON.");
        return NULL;
    }

    switch (parser->text[parser->position])
    {
    case '{':
        return parse_object(parser);
    case '[':
        return parse_array(parser);
    case '"':
        value = json_new(JSON_STRING);
        if (value == NULL)
        {
            set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing string.");
            return NULL;
        }
        value->as.string = parse_string_raw(parser);
        if (value->as.string == NULL)
        {
            json_free(value);
            return NULL;
        }
        return value;
    case 't':
        if (match_literal(parser, "true"))
        {
            value = json_new(JSON_BOOL);
            if (value == NULL)
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing bool.");
                return NULL;
            }
            value->as.boolean = 1;
            return value;
        }
        break;
    case 'f':
        if (match_literal(parser, "false"))
        {
            value = json_new(JSON_BOOL);
            if (value == NULL)
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing bool.");
                return NULL;
            }
            value->as.boolean = 0;
            return value;
        }
        break;
    case 'n':
        if (match_literal(parser, "null"))
        {
            value = json_new(JSON_NULL);
            if (value == NULL)
            {
                set_error(parser->error, GEMAP_ERROR_MEMORY, "Out of memory while parsing null.");
            }
            return value;
        }
        break;
    default:
        if (parser->text[parser->position] == '-' || isdigit((unsigned char)parser->text[parser->position]))
        {
            return parse_number(parser);
        }
        break;
    }

    set_error(parser->error, GEMAP_ERROR_PARSE, "Unexpected token at byte %zu.", parser->position);
    return NULL;
}

static json_value* json_parse(const char* text, size_t length, gemap_error* error)
{
    json_parser parser;
    json_value* root;

    parser.text = text;
    parser.length = length;
    parser.position = 0;
    parser.error = error;

    root = parse_value(&parser);
    if (root == NULL)
    {
        return NULL;
    }

    skip_ws(&parser);
    if (parser.position != parser.length)
    {
        set_error(error, GEMAP_ERROR_PARSE, "Unexpected trailing data at byte %zu.", parser.position);
        json_free(root);
        return NULL;
    }

    return root;
}

static const json_value* object_get(const json_value* object, const char* name)
{
    size_t i;

    if (object == NULL || object->type != JSON_OBJECT)
    {
        return NULL;
    }

    for (i = 0; i < object->as.object.count; i++)
    {
        if (strcmp(object->as.object.entries[i].name, name) == 0)
        {
            return object->as.object.entries[i].value;
        }
    }

    return NULL;
}

static int json_get_int(const json_value* object, const char* name, int default_value)
{
    const json_value* value = object_get(object, name);
    return value != NULL && value->type == JSON_NUMBER ? (int)value->as.number : default_value;
}

static int json_get_bool(const json_value* object, const char* name, int default_value)
{
    const json_value* value = object_get(object, name);
    return value != NULL && value->type == JSON_BOOL ? value->as.boolean : default_value;
}

static const char* json_get_string_ref(const json_value* object, const char* name)
{
    const json_value* value = object_get(object, name);
    return value != NULL && value->type == JSON_STRING ? value->as.string : NULL;
}

static char* json_dup_string_field(const json_value* object, const char* name, const char* default_value)
{
    const char* value = json_get_string_ref(object, name);
    return duplicate_string(value != NULL ? value : default_value);
}

static int gemap_stricmp(const char* a, const char* b)
{
    while (*a != '\0' && *b != '\0')
    {
        int ca = tolower((unsigned char)*a);
        int cb = tolower((unsigned char)*b);
        if (ca != cb)
        {
            return ca - cb;
        }
        a++;
        b++;
    }

    return (unsigned char)*a - (unsigned char)*b;
}

static int parse_kind_text(const char* value, gemap_tileset_kind* out_kind)
{
    if (value == NULL)
    {
        *out_kind = GEMAP_TILESET_UNKNOWN;
        return 0;
    }

    if (gemap_stricmp(value, "base") == 0)
    {
        *out_kind = GEMAP_TILESET_BASE;
        return 1;
    }

    if (gemap_stricmp(value, "advanced") == 0)
    {
        *out_kind = GEMAP_TILESET_ADVANCED;
        return 1;
    }

    *out_kind = GEMAP_TILESET_UNKNOWN;
    return 0;
}

static int parse_int_key(const char* key, int* out_value)
{
    char* end = NULL;
    long parsed;

    errno = 0;
    parsed = strtol(key, &end, 10);
    if (errno != 0 || end == key || *end != '\0')
    {
        return 0;
    }

    *out_value = (int)parsed;
    return 1;
}

static gemap_result load_int_array(const json_value* array, int** out_values, size_t* out_count, gemap_error* error)
{
    int* values;
    size_t i;

    *out_values = NULL;
    *out_count = 0;

    if (array == NULL || array->type != JSON_ARRAY || array->as.array.count == 0)
    {
        return GEMAP_OK;
    }

    values = (int*)calloc(array->as.array.count, sizeof(int));
    if (values == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading integer array.");
        return GEMAP_ERROR_MEMORY;
    }

    for (i = 0; i < array->as.array.count; i++)
    {
        const json_value* item = array->as.array.items[i];
        if (item == NULL || item->type != JSON_NUMBER)
        {
            free(values);
            set_error(error, GEMAP_ERROR_FORMAT, "Expected integer array item.");
            return GEMAP_ERROR_FORMAT;
        }

        values[i] = (int)item->as.number;
    }

    *out_values = values;
    *out_count = array->as.array.count;
    return GEMAP_OK;
}

static gemap_result load_attribute_lists(const json_value* root, gemap_map* map, gemap_error* error)
{
    const json_value* lists = object_get(root, "attributeLists");
    size_t i;

    if (lists == NULL || lists->type != JSON_ARRAY || lists->as.array.count == 0)
    {
        return GEMAP_OK;
    }

    map->attribute_lists = (gemap_attribute_list*)calloc(lists->as.array.count, sizeof(gemap_attribute_list));
    if (map->attribute_lists == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading attribute lists.");
        return GEMAP_ERROR_MEMORY;
    }

    map->attribute_list_count = lists->as.array.count;
    for (i = 0; i < lists->as.array.count; i++)
    {
        const json_value* item = lists->as.array.items[i];
        const json_value* values;
        gemap_attribute_list* list;
        size_t j;

        if (item == NULL || item->type != JSON_OBJECT)
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Attribute list must be an object.");
            return GEMAP_ERROR_FORMAT;
        }

        list = &map->attribute_lists[i];
        list->id = json_dup_string_field(item, "id", "");
        list->name = json_dup_string_field(item, "name", "");
        if (list->id == NULL || list->name == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading attribute list.");
            return GEMAP_ERROR_MEMORY;
        }

        values = object_get(item, "values");
        if (values == NULL || values->type != JSON_ARRAY || values->as.array.count == 0)
        {
            continue;
        }

        list->values = (gemap_attribute_value*)calloc(values->as.array.count, sizeof(gemap_attribute_value));
        if (list->values == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading attribute values.");
            return GEMAP_ERROR_MEMORY;
        }

        list->value_count = values->as.array.count;
        for (j = 0; j < values->as.array.count; j++)
        {
            const json_value* value_object = values->as.array.items[j];
            gemap_attribute_value* value;

            if (value_object == NULL || value_object->type != JSON_OBJECT)
            {
                set_error(error, GEMAP_ERROR_FORMAT, "Attribute value must be an object.");
                return GEMAP_ERROR_FORMAT;
            }

            value = &list->values[j];
            value->value = json_get_int(value_object, "value", 0);
            value->name = json_dup_string_field(value_object, "name", "");
            value->display_text = json_dup_string_field(value_object, "displayText", "");
            value->memo = json_dup_string_field(value_object, "memo", "");
            value->color = json_dup_string_field(value_object, "color", NULL);
            if (value->name == NULL || value->display_text == NULL || value->memo == NULL)
            {
                set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading attribute value.");
                return GEMAP_ERROR_MEMORY;
            }
        }
    }

    return GEMAP_OK;
}

static gemap_result load_tile_attribute_sets(const json_value* object, gemap_tileset* tileset, gemap_error* error)
{
    const json_value* attributes = object_get(object, "tileAttributes");
    size_t i;

    if (attributes == NULL || attributes->type != JSON_OBJECT || attributes->as.object.count == 0)
    {
        return GEMAP_OK;
    }

    tileset->tile_attributes = (gemap_tile_attribute_set*)calloc(attributes->as.object.count, sizeof(gemap_tile_attribute_set));
    if (tileset->tile_attributes == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading tile attributes.");
        return GEMAP_ERROR_MEMORY;
    }

    tileset->tile_attribute_count = attributes->as.object.count;
    for (i = 0; i < attributes->as.object.count; i++)
    {
        json_object_entry* entry = &attributes->as.object.entries[i];
        gemap_tile_attribute_set* set = &tileset->tile_attributes[i];
        gemap_result result;

        if (!parse_int_key(entry->name, &set->tile_id))
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Invalid tile attribute key: %s.", entry->name);
            return GEMAP_ERROR_FORMAT;
        }

        result = load_int_array(entry->value, &set->values, &set->value_count, error);
        if (result != GEMAP_OK)
        {
            return result;
        }
    }

    return GEMAP_OK;
}

static gemap_result load_tile_priorities(const json_value* object, gemap_tileset* tileset, gemap_error* error)
{
    const json_value* priorities = object_get(object, "tilePriorities");
    size_t i;

    if (priorities == NULL || priorities->type != JSON_OBJECT || priorities->as.object.count == 0)
    {
        return GEMAP_OK;
    }

    tileset->tile_priorities = (gemap_tile_priority*)calloc(priorities->as.object.count, sizeof(gemap_tile_priority));
    if (tileset->tile_priorities == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading tile priorities.");
        return GEMAP_ERROR_MEMORY;
    }

    tileset->tile_priority_count = priorities->as.object.count;
    for (i = 0; i < priorities->as.object.count; i++)
    {
        json_object_entry* entry = &priorities->as.object.entries[i];
        const json_value* value = entry->value;
        if (!parse_int_key(entry->name, &tileset->tile_priorities[i].tile_id))
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Invalid tile priority key: %s.", entry->name);
            return GEMAP_ERROR_FORMAT;
        }

        if (value == NULL || value->type != JSON_NUMBER)
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Tile priority value must be a number.");
            return GEMAP_ERROR_FORMAT;
        }

        tileset->tile_priorities[i].priority = (int)value->as.number;
    }

    return GEMAP_OK;
}

static gemap_result load_tilesets(const json_value* root, gemap_map* map, gemap_error* error)
{
    const json_value* tilesets = object_get(root, "tileSets");
    size_t i;

    if (tilesets == NULL || tilesets->type != JSON_ARRAY || tilesets->as.array.count == 0)
    {
        return GEMAP_OK;
    }

    map->tilesets = (gemap_tileset*)calloc(tilesets->as.array.count, sizeof(gemap_tileset));
    if (map->tilesets == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading tile sets.");
        return GEMAP_ERROR_MEMORY;
    }

    map->tileset_count = tilesets->as.array.count;
    for (i = 0; i < tilesets->as.array.count; i++)
    {
        const json_value* item = tilesets->as.array.items[i];
        const char* kind_text;
        gemap_tileset* tileset;
        gemap_result result;

        if (item == NULL || item->type != JSON_OBJECT)
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Tile set must be an object.");
            return GEMAP_ERROR_FORMAT;
        }

        tileset = &map->tilesets[i];
        tileset->id = json_dup_string_field(item, "id", "");
        tileset->name = json_dup_string_field(item, "name", "");
        tileset->image = json_dup_string_field(item, "image", "");
        tileset->transparent_color = json_dup_string_field(item, "transparentColor", NULL);
        tileset->attribute_list_id = json_dup_string_field(item, "attributeListId", NULL);
        if (tileset->id == NULL || tileset->name == NULL || tileset->image == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading tile set.");
            return GEMAP_ERROR_MEMORY;
        }

        kind_text = json_get_string_ref(item, "kind");
        if (!parse_kind_text(kind_text, &tileset->kind))
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Unsupported tile set kind: %s.", kind_text != NULL ? kind_text : "");
            return GEMAP_ERROR_FORMAT;
        }

        tileset->tile_size = json_get_int(item, "tileSize", map->tile_size);
        if (tileset->tile_size != map->tile_size)
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Different tile sizes cannot be mixed in the same map.");
            return GEMAP_ERROR_FORMAT;
        }

        result = load_tile_attribute_sets(item, tileset, error);
        if (result != GEMAP_OK)
        {
            return result;
        }

        result = load_tile_priorities(item, tileset, error);
        if (result != GEMAP_OK)
        {
            return result;
        }
    }

    return GEMAP_OK;
}

static gemap_result load_tile(const json_value* object, gemap_tile* tile, gemap_error* error)
{
    const json_value* attributes;
    const json_value* legacy_attribute;
    gemap_result result;

    if (object == NULL || object->type != JSON_OBJECT)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Layer tile must be an object.");
        return GEMAP_ERROR_FORMAT;
    }

    tile->x = json_get_int(object, "x", 0);
    tile->y = json_get_int(object, "y", 0);
    tile->tile_id = json_get_int(object, "tileId", 0);
    tile->tile_set_id = json_dup_string_field(object, "tileSetId", "");
    tile->display_priority = json_get_int(object, "displayPriority", 0);
    if (tile->tile_set_id == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading tile.");
        return GEMAP_ERROR_MEMORY;
    }

    attributes = object_get(object, "attributeValues");
    result = load_int_array(attributes, &tile->attribute_values, &tile->attribute_value_count, error);
    if (result != GEMAP_OK)
    {
        return result;
    }

    legacy_attribute = object_get(object, "attribute");
    if (tile->attribute_value_count == 0 && legacy_attribute != NULL && legacy_attribute->type == JSON_NUMBER)
    {
        tile->attribute_values = (int*)calloc(1, sizeof(int));
        if (tile->attribute_values == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading legacy tile attribute.");
            return GEMAP_ERROR_MEMORY;
        }

        tile->attribute_values[0] = (int)legacy_attribute->as.number;
        tile->attribute_value_count = 1;
    }

    return GEMAP_OK;
}

static gemap_result load_layers(const json_value* root, gemap_map* map, gemap_error* error)
{
    const json_value* layers = object_get(root, "layers");
    size_t i;

    if (layers == NULL || layers->type != JSON_ARRAY || layers->as.array.count == 0)
    {
        return GEMAP_OK;
    }

    map->layers = (gemap_layer*)calloc(layers->as.array.count, sizeof(gemap_layer));
    if (map->layers == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading layers.");
        return GEMAP_ERROR_MEMORY;
    }

    map->layer_count = layers->as.array.count;
    for (i = 0; i < layers->as.array.count; i++)
    {
        const json_value* item = layers->as.array.items[i];
        const json_value* tiles;
        const char* kind_text;
        gemap_layer* layer;
        size_t j;

        if (item == NULL || item->type != JSON_OBJECT)
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Layer must be an object.");
            return GEMAP_ERROR_FORMAT;
        }

        layer = &map->layers[i];
        layer->id = json_dup_string_field(item, "id", "");
        layer->name = json_dup_string_field(item, "name", "");
        if (layer->id == NULL || layer->name == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading layer.");
            return GEMAP_ERROR_MEMORY;
        }

        kind_text = json_get_string_ref(item, "kind");
        if (!parse_kind_text(kind_text, &layer->kind))
        {
            set_error(error, GEMAP_ERROR_FORMAT, "Unsupported layer kind: %s.", kind_text != NULL ? kind_text : "");
            return GEMAP_ERROR_FORMAT;
        }

        layer->visible = json_get_bool(item, "visible", 1);
        layer->locked = json_get_bool(item, "locked", 0);
        layer->z_index = json_get_int(item, "zIndex", 0);

        tiles = object_get(item, "tiles");
        if (tiles == NULL || tiles->type != JSON_ARRAY || tiles->as.array.count == 0)
        {
            continue;
        }

        layer->tiles = (gemap_tile*)calloc(tiles->as.array.count, sizeof(gemap_tile));
        if (layer->tiles == NULL)
        {
            set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading layer tiles.");
            return GEMAP_ERROR_MEMORY;
        }

        layer->tile_count = tiles->as.array.count;
        for (j = 0; j < tiles->as.array.count; j++)
        {
            gemap_result result = load_tile(tiles->as.array.items[j], &layer->tiles[j], error);
            if (result != GEMAP_OK)
            {
                return result;
            }
        }
    }

    return GEMAP_OK;
}

static gemap_result load_map_data(const json_value* root, gemap_map* map, gemap_error* error)
{
    const json_value* header = object_get(root, "header");
    const json_value* map_object = object_get(root, "map");
    gemap_result result;

    if (root == NULL || root->type != JSON_OBJECT)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Root JSON value must be an object.");
        return GEMAP_ERROR_FORMAT;
    }

    if (header == NULL || header->type != JSON_OBJECT)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Missing map header.");
        return GEMAP_ERROR_FORMAT;
    }

    if (map_object == NULL || map_object->type != JSON_OBJECT)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Missing map object.");
        return GEMAP_ERROR_FORMAT;
    }

    map->format = json_dup_string_field(header, "format", "");
    map->schema_version = json_get_int(header, "schemaVersion", 0);
    map->name = json_dup_string_field(map_object, "name", "Untitled");
    map->active_attribute_list_id = json_dup_string_field(map_object, "activeAttributeListId", NULL);
    if (map->format == NULL || map->name == NULL)
    {
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while loading map metadata.");
        return GEMAP_ERROR_MEMORY;
    }

    if (gemap_stricmp(map->format, "gameEditor.map") != 0)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Unsupported map format: %s.", map->format);
        return GEMAP_ERROR_FORMAT;
    }

    if (map->schema_version > 1)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Unsupported schema version: %d.", map->schema_version);
        return GEMAP_ERROR_FORMAT;
    }

    map->width = json_get_int(map_object, "width", 0);
    map->height = json_get_int(map_object, "height", 0);
    map->tile_size = json_get_int(map_object, "tileSize", 0);
    if (map->width <= 0 || map->height <= 0 || map->tile_size <= 0)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Map size or tile size is invalid.");
        return GEMAP_ERROR_FORMAT;
    }

    result = load_attribute_lists(root, map, error);
    if (result != GEMAP_OK)
    {
        return result;
    }

    result = load_tilesets(root, map, error);
    if (result != GEMAP_OK)
    {
        return result;
    }

    return load_layers(root, map, error);
}

gemap_result gemap_load_json(const char* json, size_t length, gemap_map** out_map, gemap_error* error)
{
    json_value* root;
    gemap_map* map;
    gemap_result result;

    if (error != NULL)
    {
        error->code = GEMAP_OK;
        error->message[0] = '\0';
    }

    if (out_map == NULL)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "Output map pointer is null.");
        return GEMAP_ERROR_FORMAT;
    }

    *out_map = NULL;
    if (json == NULL)
    {
        set_error(error, GEMAP_ERROR_FORMAT, "JSON pointer is null.");
        return GEMAP_ERROR_FORMAT;
    }

    root = json_parse(json, length, error);
    if (root == NULL)
    {
        return error != NULL ? error->code : GEMAP_ERROR_PARSE;
    }

    map = (gemap_map*)calloc(1, sizeof(gemap_map));
    if (map == NULL)
    {
        json_free(root);
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while creating map.");
        return GEMAP_ERROR_MEMORY;
    }

    result = load_map_data(root, map, error);
    json_free(root);
    if (result != GEMAP_OK)
    {
        gemap_free(map);
        return result;
    }

    *out_map = map;
    return GEMAP_OK;
}

gemap_result gemap_load_file(const char* path, gemap_map** out_map, gemap_error* error)
{
    FILE* file;
    char* buffer;
    long length;
    size_t read_count;
    gemap_result result;

    if (error != NULL)
    {
        error->code = GEMAP_OK;
        error->message[0] = '\0';
    }

    if (path == NULL)
    {
        set_error(error, GEMAP_ERROR_IO, "Path is null.");
        return GEMAP_ERROR_IO;
    }

    file = fopen(path, "rb");
    if (file == NULL)
    {
        set_error(error, GEMAP_ERROR_IO, "Failed to open file: %s.", path);
        return GEMAP_ERROR_IO;
    }

    if (fseek(file, 0, SEEK_END) != 0)
    {
        fclose(file);
        set_error(error, GEMAP_ERROR_IO, "Failed to seek file: %s.", path);
        return GEMAP_ERROR_IO;
    }

    length = ftell(file);
    if (length < 0)
    {
        fclose(file);
        set_error(error, GEMAP_ERROR_IO, "Failed to measure file: %s.", path);
        return GEMAP_ERROR_IO;
    }

    if (fseek(file, 0, SEEK_SET) != 0)
    {
        fclose(file);
        set_error(error, GEMAP_ERROR_IO, "Failed to rewind file: %s.", path);
        return GEMAP_ERROR_IO;
    }

    buffer = (char*)malloc((size_t)length + 1);
    if (buffer == NULL)
    {
        fclose(file);
        set_error(error, GEMAP_ERROR_MEMORY, "Out of memory while reading file.");
        return GEMAP_ERROR_MEMORY;
    }

    read_count = fread(buffer, 1, (size_t)length, file);
    fclose(file);
    if (read_count != (size_t)length)
    {
        free(buffer);
        set_error(error, GEMAP_ERROR_IO, "Failed to read file: %s.", path);
        return GEMAP_ERROR_IO;
    }

    buffer[(size_t)length] = '\0';
    result = gemap_load_json(buffer, (size_t)length, out_map, error);
    free(buffer);
    return result;
}

void gemap_free(gemap_map* map)
{
    size_t i;

    if (map == NULL)
    {
        return;
    }

    free(map->format);
    free(map->name);
    free(map->active_attribute_list_id);

    for (i = 0; i < map->attribute_list_count; i++)
    {
        size_t j;
        gemap_attribute_list* list = &map->attribute_lists[i];
        free(list->id);
        free(list->name);
        for (j = 0; j < list->value_count; j++)
        {
            free(list->values[j].name);
            free(list->values[j].display_text);
            free(list->values[j].memo);
            free(list->values[j].color);
        }
        free(list->values);
    }
    free(map->attribute_lists);

    for (i = 0; i < map->tileset_count; i++)
    {
        size_t j;
        gemap_tileset* tileset = &map->tilesets[i];
        free(tileset->id);
        free(tileset->name);
        free(tileset->image);
        free(tileset->transparent_color);
        free(tileset->attribute_list_id);
        for (j = 0; j < tileset->tile_attribute_count; j++)
        {
            free(tileset->tile_attributes[j].values);
        }
        free(tileset->tile_attributes);
        free(tileset->tile_priorities);
    }
    free(map->tilesets);

    for (i = 0; i < map->layer_count; i++)
    {
        size_t j;
        gemap_layer* layer = &map->layers[i];
        free(layer->id);
        free(layer->name);
        for (j = 0; j < layer->tile_count; j++)
        {
            free(layer->tiles[j].tile_set_id);
            free(layer->tiles[j].attribute_values);
        }
        free(layer->tiles);
    }
    free(map->layers);
    free(map);
}

const char* gemap_result_name(gemap_result result)
{
    switch (result)
    {
    case GEMAP_OK:
        return "ok";
    case GEMAP_ERROR_IO:
        return "io_error";
    case GEMAP_ERROR_PARSE:
        return "parse_error";
    case GEMAP_ERROR_FORMAT:
        return "format_error";
    case GEMAP_ERROR_MEMORY:
        return "memory_error";
    default:
        return "unknown_error";
    }
}

const char* gemap_tileset_kind_name(gemap_tileset_kind kind)
{
    switch (kind)
    {
    case GEMAP_TILESET_BASE:
        return "base";
    case GEMAP_TILESET_ADVANCED:
        return "advanced";
    default:
        return "unknown";
    }
}

const gemap_tileset* gemap_find_tileset(const gemap_map* map, const char* id)
{
    size_t i;

    if (map == NULL || id == NULL)
    {
        return NULL;
    }

    for (i = 0; i < map->tileset_count; i++)
    {
        if (map->tilesets[i].id != NULL && strcmp(map->tilesets[i].id, id) == 0)
        {
            return &map->tilesets[i];
        }
    }

    return NULL;
}

const gemap_layer* gemap_find_layer(const gemap_map* map, const char* id)
{
    size_t i;

    if (map == NULL || id == NULL)
    {
        return NULL;
    }

    for (i = 0; i < map->layer_count; i++)
    {
        if (map->layers[i].id != NULL && strcmp(map->layers[i].id, id) == 0)
        {
            return &map->layers[i];
        }
    }

    return NULL;
}

const gemap_tile* gemap_layer_find_tile(const gemap_layer* layer, int x, int y)
{
    size_t i;

    if (layer == NULL)
    {
        return NULL;
    }

    for (i = 0; i < layer->tile_count; i++)
    {
        if (layer->tiles[i].x == x && layer->tiles[i].y == y)
        {
            return &layer->tiles[i];
        }
    }

    return NULL;
}
