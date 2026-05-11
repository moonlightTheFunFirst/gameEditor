#include "gemap/gemap.h"

#ifdef _WIN32

#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include <shellapi.h>
#include <windowsx.h>

#define ID_MODE_COMBO 1001
#define ID_LAYER_COMBO 1002
#define ID_GRID_CHECK 1003
#define TOOLBAR_HEIGHT 40
#define STATUS_HEIGHT 24
#define CONTROL_HEIGHT 24
#define CONTROL_MARGIN 8

typedef enum preview_mode
{
    PREVIEW_MODE_NORMAL = 0,
    PREVIEW_MODE_ATTRIBUTES = 1,
    PREVIEW_MODE_PRIORITY = 2
} preview_mode;

typedef struct preview_tileset_image
{
    const gemap_tileset* tileset;
    HBITMAP bitmap;
    int width;
    int height;
    int columns;
    int rows;
    int has_transparent;
    COLORREF transparent_color;
    wchar_t* resolved_path;
} preview_tileset_image;

typedef struct preview_app
{
    HINSTANCE instance;
    HWND window;
    HWND mode_combo;
    HWND layer_combo;
    HWND grid_check;
    HWND status;
    HFONT ui_font;
    HFONT label_font;
    HDC dim_dc;
    HBITMAP dim_bitmap;
    HBITMAP old_dim_bitmap;
    gemap_map* map;
    preview_tileset_image* images;
    size_t image_count;
    wchar_t* map_path;
    wchar_t* map_dir;
    wchar_t* asset_root;
    wchar_t* exe_dir;
    preview_mode mode;
    gemap_tileset_kind overlay_kind;
    int show_grid;
    int scroll_x;
    int scroll_y;
    int client_width;
    int client_height;
} preview_app;

static int max_int(int a, int b)
{
    return a > b ? a : b;
}

static int min_int(int a, int b)
{
    return a < b ? a : b;
}

static int clamp_int(int value, int min_value, int max_value)
{
    if (value < min_value)
    {
        return min_value;
    }

    if (value > max_value)
    {
        return max_value;
    }

    return value;
}

static wchar_t* duplicate_wide(const wchar_t* value)
{
    size_t length;
    wchar_t* result;

    if (value == NULL)
    {
        return NULL;
    }

    length = wcslen(value);
    result = (wchar_t*)malloc((length + 1) * sizeof(wchar_t));
    if (result == NULL)
    {
        return NULL;
    }

    memcpy(result, value, (length + 1) * sizeof(wchar_t));
    return result;
}

static wchar_t* utf8_to_wide(const char* value)
{
    int length;
    wchar_t* result;

    if (value == NULL)
    {
        return duplicate_wide(L"");
    }

    length = MultiByteToWideChar(CP_UTF8, 0, value, -1, NULL, 0);
    if (length <= 0)
    {
        length = MultiByteToWideChar(CP_ACP, 0, value, -1, NULL, 0);
        if (length <= 0)
        {
            return duplicate_wide(L"");
        }

        result = (wchar_t*)malloc((size_t)length * sizeof(wchar_t));
        if (result == NULL)
        {
            return NULL;
        }

        MultiByteToWideChar(CP_ACP, 0, value, -1, result, length);
        return result;
    }

    result = (wchar_t*)malloc((size_t)length * sizeof(wchar_t));
    if (result == NULL)
    {
        return NULL;
    }

    MultiByteToWideChar(CP_UTF8, 0, value, -1, result, length);
    return result;
}

static char* wide_to_utf8(const wchar_t* value)
{
    int length;
    char* result;

    if (value == NULL)
    {
        return NULL;
    }

    length = WideCharToMultiByte(CP_UTF8, 0, value, -1, NULL, 0, NULL, NULL);
    if (length <= 0)
    {
        return NULL;
    }

    result = (char*)malloc((size_t)length);
    if (result == NULL)
    {
        return NULL;
    }

    WideCharToMultiByte(CP_UTF8, 0, value, -1, result, length, NULL, NULL);
    return result;
}

static int file_exists(const wchar_t* path)
{
    DWORD attributes;

    if (path == NULL || path[0] == L'\0')
    {
        return 0;
    }

    attributes = GetFileAttributesW(path);
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

static int is_rooted_path(const wchar_t* path)
{
    if (path == NULL || path[0] == L'\0')
    {
        return 0;
    }

    if ((path[0] == L'\\' && path[1] == L'\\') || path[0] == L'/' || path[0] == L'\\')
    {
        return 1;
    }

    return ((path[0] >= L'A' && path[0] <= L'Z') || (path[0] >= L'a' && path[0] <= L'z')) && path[1] == L':';
}

static wchar_t* join_path(const wchar_t* left, const wchar_t* right)
{
    size_t left_length;
    size_t right_length;
    int needs_separator;
    wchar_t* result;

    if (right == NULL)
    {
        return duplicate_wide(left);
    }

    if (left == NULL || left[0] == L'\0' || is_rooted_path(right))
    {
        return duplicate_wide(right);
    }

    left_length = wcslen(left);
    right_length = wcslen(right);
    needs_separator = left_length > 0 && left[left_length - 1] != L'\\' && left[left_length - 1] != L'/';
    result = (wchar_t*)malloc((left_length + right_length + (needs_separator ? 2u : 1u)) * sizeof(wchar_t));
    if (result == NULL)
    {
        return NULL;
    }

    memcpy(result, left, left_length * sizeof(wchar_t));
    if (needs_separator)
    {
        result[left_length++] = L'\\';
    }

    memcpy(result + left_length, right, (right_length + 1) * sizeof(wchar_t));
    return result;
}

static wchar_t* get_directory_name(const wchar_t* path)
{
    const wchar_t* slash;
    const wchar_t* backslash;
    const wchar_t* last;
    size_t length;
    wchar_t* result;

    if (path == NULL)
    {
        return NULL;
    }

    slash = wcsrchr(path, L'/');
    backslash = wcsrchr(path, L'\\');
    last = slash > backslash ? slash : backslash;
    if (last == NULL)
    {
        return duplicate_wide(L".");
    }

    length = (size_t)(last - path);
    if (length == 0)
    {
        length = 1;
    }

    result = (wchar_t*)malloc((length + 1) * sizeof(wchar_t));
    if (result == NULL)
    {
        return NULL;
    }

    memcpy(result, path, length * sizeof(wchar_t));
    result[length] = L'\0';
    return result;
}

static wchar_t* get_parent_directory(const wchar_t* path)
{
    wchar_t* copy;
    wchar_t* slash;
    wchar_t* backslash;
    wchar_t* last;

    copy = duplicate_wide(path);
    if (copy == NULL)
    {
        return NULL;
    }

    slash = wcsrchr(copy, L'/');
    backslash = wcsrchr(copy, L'\\');
    last = slash > backslash ? slash : backslash;
    if (last == NULL || last == copy)
    {
        free(copy);
        return NULL;
    }

    *last = L'\0';
    return copy;
}

static wchar_t* get_current_directory_alloc(void)
{
    DWORD length = GetCurrentDirectoryW(0, NULL);
    wchar_t* result;

    if (length == 0)
    {
        return NULL;
    }

    result = (wchar_t*)malloc((size_t)length * sizeof(wchar_t));
    if (result == NULL)
    {
        return NULL;
    }

    if (GetCurrentDirectoryW(length, result) == 0)
    {
        free(result);
        return NULL;
    }

    return result;
}

static wchar_t* get_executable_directory(void)
{
    DWORD capacity = 260;
    wchar_t* buffer = NULL;

    for (;;)
    {
        DWORD length;
        wchar_t* grown = (wchar_t*)realloc(buffer, (size_t)capacity * sizeof(wchar_t));
        if (grown == NULL)
        {
            free(buffer);
            return NULL;
        }

        buffer = grown;
        length = GetModuleFileNameW(NULL, buffer, capacity);
        if (length == 0)
        {
            free(buffer);
            return NULL;
        }

        if (length < capacity - 1)
        {
            wchar_t* directory = get_directory_name(buffer);
            free(buffer);
            return directory;
        }

        capacity *= 2;
    }
}

static wchar_t* find_file_in_ancestors(const wchar_t* start_directory, const wchar_t* relative_path)
{
    wchar_t* current;

    if (start_directory == NULL || relative_path == NULL)
    {
        return NULL;
    }

    current = duplicate_wide(start_directory);
    while (current != NULL)
    {
        wchar_t* candidate = join_path(current, relative_path);
        wchar_t* parent;
        if (candidate != NULL && file_exists(candidate))
        {
            free(current);
            return candidate;
        }

        free(candidate);
        parent = get_parent_directory(current);
        free(current);
        current = parent;
    }

    return NULL;
}

static wchar_t* resolve_image_path(const preview_app* app, const char* image_path_utf8)
{
    wchar_t* image_path = utf8_to_wide(image_path_utf8);
    wchar_t* candidate;
    wchar_t* cwd;

    if (image_path == NULL)
    {
        return NULL;
    }

    if (is_rooted_path(image_path) && file_exists(image_path))
    {
        return image_path;
    }

    candidate = join_path(app->map_dir, image_path);
    if (candidate != NULL && file_exists(candidate))
    {
        free(image_path);
        return candidate;
    }
    free(candidate);

    candidate = join_path(app->asset_root, image_path);
    if (candidate != NULL && file_exists(candidate))
    {
        free(image_path);
        return candidate;
    }
    free(candidate);

    candidate = join_path(app->exe_dir, image_path);
    if (candidate != NULL && file_exists(candidate))
    {
        free(image_path);
        return candidate;
    }
    free(candidate);

    cwd = get_current_directory_alloc();
    candidate = join_path(cwd, image_path);
    if (candidate != NULL && file_exists(candidate))
    {
        free(cwd);
        free(image_path);
        return candidate;
    }
    free(candidate);

    candidate = find_file_in_ancestors(app->exe_dir, image_path);
    if (candidate != NULL)
    {
        free(cwd);
        free(image_path);
        return candidate;
    }

    candidate = find_file_in_ancestors(cwd, image_path);
    free(cwd);
    free(image_path);
    return candidate;
}

static int parse_hex_color(const char* text, COLORREF* color)
{
    unsigned int r;
    unsigned int g;
    unsigned int b;

    if (text == NULL || text[0] == '\0')
    {
        return 0;
    }

    if (text[0] == '#')
    {
        text++;
    }

    if (strlen(text) != 6)
    {
        return 0;
    }

    if (sscanf_s(text, "%02x%02x%02x", &r, &g, &b) != 3)
    {
        return 0;
    }

    *color = RGB(r, g, b);
    return 1;
}

static gemap_result load_map_file_w(const wchar_t* path, gemap_map** out_map, gemap_error* error)
{
    FILE* file;
    char* buffer;
    long length;
    size_t read_count;
    gemap_result result;

    *out_map = NULL;
    file = _wfopen(path, L"rb");
    if (file == NULL)
    {
        if (error != NULL)
        {
            error->code = GEMAP_ERROR_IO;
            snprintf(error->message, sizeof(error->message), "Failed to open map file.");
        }
        return GEMAP_ERROR_IO;
    }

    if (fseek(file, 0, SEEK_END) != 0)
    {
        fclose(file);
        return GEMAP_ERROR_IO;
    }

    length = ftell(file);
    if (length < 0)
    {
        fclose(file);
        return GEMAP_ERROR_IO;
    }

    if (fseek(file, 0, SEEK_SET) != 0)
    {
        fclose(file);
        return GEMAP_ERROR_IO;
    }

    buffer = (char*)malloc((size_t)length + 1);
    if (buffer == NULL)
    {
        fclose(file);
        return GEMAP_ERROR_MEMORY;
    }

    read_count = fread(buffer, 1, (size_t)length, file);
    fclose(file);
    if (read_count != (size_t)length)
    {
        free(buffer);
        return GEMAP_ERROR_IO;
    }

    buffer[(size_t)length] = '\0';
    result = gemap_load_json(buffer, (size_t)length, out_map, error);
    free(buffer);
    return result;
}

static const gemap_attribute_list* find_attribute_list(const gemap_map* map, const char* id)
{
    size_t i;

    if (map == NULL || map->attribute_list_count == 0)
    {
        return NULL;
    }

    if (id != NULL)
    {
        for (i = 0; i < map->attribute_list_count; i++)
        {
            if (map->attribute_lists[i].id != NULL && strcmp(map->attribute_lists[i].id, id) == 0)
            {
                return &map->attribute_lists[i];
            }
        }
    }

    return &map->attribute_lists[0];
}

static const gemap_attribute_value* find_attribute_value(const gemap_attribute_list* list, int value)
{
    size_t i;

    if (list == NULL)
    {
        return NULL;
    }

    for (i = 0; i < list->value_count; i++)
    {
        if (list->values[i].value == value)
        {
            return &list->values[i];
        }
    }

    return NULL;
}

static const gemap_layer* find_layer_by_kind(const gemap_map* map, gemap_tileset_kind kind)
{
    size_t i;

    if (map == NULL)
    {
        return NULL;
    }

    for (i = 0; i < map->layer_count; i++)
    {
        if (map->layers[i].kind == kind)
        {
            return &map->layers[i];
        }
    }

    return NULL;
}

static const preview_tileset_image* find_image_by_tileset_id(const preview_app* app, const char* id)
{
    size_t i;

    if (app == NULL || id == NULL)
    {
        return NULL;
    }

    for (i = 0; i < app->image_count; i++)
    {
        if (app->images[i].tileset != NULL
            && app->images[i].tileset->id != NULL
            && strcmp(app->images[i].tileset->id, id) == 0)
        {
            return &app->images[i];
        }
    }

    return NULL;
}

static const gemap_tile* find_top_tile_at(const preview_app* app, int x, int y, gemap_tileset_kind* out_kind)
{
    const gemap_layer* advanced = find_layer_by_kind(app->map, GEMAP_TILESET_ADVANCED);
    const gemap_layer* base = find_layer_by_kind(app->map, GEMAP_TILESET_BASE);
    const gemap_tile* tile;

    tile = gemap_layer_find_tile(advanced, x, y);
    if (tile != NULL)
    {
        if (out_kind != NULL)
        {
            *out_kind = GEMAP_TILESET_ADVANCED;
        }
        return tile;
    }

    tile = gemap_layer_find_tile(base, x, y);
    if (tile != NULL && out_kind != NULL)
    {
        *out_kind = GEMAP_TILESET_BASE;
    }

    return tile;
}

static const wchar_t* kind_label(gemap_tileset_kind kind)
{
    return kind == GEMAP_TILESET_ADVANCED ? L"Advanced" : L"Base";
}

static void append_text(wchar_t* buffer, size_t capacity, const wchar_t* text)
{
    size_t current;
    size_t available;

    if (buffer == NULL || capacity == 0 || text == NULL)
    {
        return;
    }

    current = wcslen(buffer);
    if (current >= capacity - 1)
    {
        return;
    }

    available = capacity - current - 1;
    wcsncpy_s(buffer + current, available + 1, text, available);
}

static void append_utf8_text(wchar_t* buffer, size_t capacity, const char* text)
{
    wchar_t* wide = utf8_to_wide(text);
    if (wide != NULL)
    {
        append_text(buffer, capacity, wide);
        free(wide);
    }
}

static void format_attribute_label(
    const gemap_attribute_list* list,
    const int* values,
    size_t value_count,
    wchar_t* buffer,
    size_t capacity)
{
    size_t i;

    if (buffer == NULL || capacity == 0)
    {
        return;
    }

    buffer[0] = L'\0';
    for (i = 0; i < value_count; i++)
    {
        const gemap_attribute_value* definition = find_attribute_value(list, values[i]);
        if (definition != NULL)
        {
            if (definition->display_text != NULL && definition->display_text[0] != '\0')
            {
                append_utf8_text(buffer, capacity, definition->display_text);
            }
            else if (definition->name != NULL && definition->name[0] != '\0')
            {
                wchar_t* name = utf8_to_wide(definition->name);
                if (name != NULL)
                {
                    wchar_t first[2] = { name[0], L'\0' };
                    append_text(buffer, capacity, first);
                    free(name);
                }
            }
        }
        else
        {
            wchar_t number[24];
            swprintf_s(number, sizeof(number) / sizeof(number[0]), L"%d", values[i]);
            append_text(buffer, capacity, number);
        }
    }
}

static void format_int_list(const int* values, size_t count, wchar_t* buffer, size_t capacity)
{
    size_t i;

    if (buffer == NULL || capacity == 0)
    {
        return;
    }

    buffer[0] = L'\0';
    append_text(buffer, capacity, L"[");
    for (i = 0; i < count; i++)
    {
        wchar_t number[32];
        if (i > 0)
        {
            append_text(buffer, capacity, L",");
        }
        swprintf_s(number, sizeof(number) / sizeof(number[0]), L"%d", values[i]);
        append_text(buffer, capacity, number);
    }
    append_text(buffer, capacity, L"]");
}

static RECT map_view_rect(const preview_app* app)
{
    RECT rect;
    rect.left = 0;
    rect.top = TOOLBAR_HEIGHT;
    rect.right = app->client_width;
    rect.bottom = max_int(TOOLBAR_HEIGHT, app->client_height - STATUS_HEIGHT);
    return rect;
}

static int map_pixel_width(const preview_app* app)
{
    return app->map != NULL ? app->map->width * app->map->tile_size : 0;
}

static int map_pixel_height(const preview_app* app)
{
    return app->map != NULL ? app->map->height * app->map->tile_size : 0;
}

static void update_scroll_bars(preview_app* app)
{
    RECT view = map_view_rect(app);
    int view_width = max_int(1, view.right - view.left);
    int view_height = max_int(1, view.bottom - view.top);
    int max_x = max_int(0, map_pixel_width(app) - view_width);
    int max_y = max_int(0, map_pixel_height(app) - view_height);
    SCROLLINFO info;

    app->scroll_x = clamp_int(app->scroll_x, 0, max_x);
    app->scroll_y = clamp_int(app->scroll_y, 0, max_y);

    memset(&info, 0, sizeof(info));
    info.cbSize = sizeof(info);
    info.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
    info.nMin = 0;
    info.nMax = max_int(0, map_pixel_width(app) - 1);
    info.nPage = (UINT)view_width;
    info.nPos = app->scroll_x;
    SetScrollInfo(app->window, SB_HORZ, &info, TRUE);

    info.nMax = max_int(0, map_pixel_height(app) - 1);
    info.nPage = (UINT)view_height;
    info.nPos = app->scroll_y;
    SetScrollInfo(app->window, SB_VERT, &info, TRUE);
}

static void scroll_window_to(preview_app* app, int x, int y)
{
    RECT view = map_view_rect(app);
    int old_x = app->scroll_x;
    int old_y = app->scroll_y;
    int view_width = max_int(1, view.right - view.left);
    int view_height = max_int(1, view.bottom - view.top);

    app->scroll_x = clamp_int(x, 0, max_int(0, map_pixel_width(app) - view_width));
    app->scroll_y = clamp_int(y, 0, max_int(0, map_pixel_height(app) - view_height));
    if (old_x == app->scroll_x && old_y == app->scroll_y)
    {
        return;
    }

    update_scroll_bars(app);
    InvalidateRect(app->window, &view, FALSE);
}

static int scroll_from_request(HWND hwnd, int bar, WPARAM wParam, int current, int page, int maximum)
{
    int result = current;
    SCROLLINFO info;

    switch (LOWORD(wParam))
    {
    case SB_LINELEFT:
        result -= 32;
        break;
    case SB_LINERIGHT:
        result += 32;
        break;
    case SB_PAGELEFT:
        result -= page;
        break;
    case SB_PAGERIGHT:
        result += page;
        break;
    case SB_THUMBTRACK:
    case SB_THUMBPOSITION:
        memset(&info, 0, sizeof(info));
        info.cbSize = sizeof(info);
        info.fMask = SIF_TRACKPOS;
        GetScrollInfo(hwnd, bar, &info);
        result = info.nTrackPos;
        break;
    default:
        break;
    }

    return clamp_int(result, 0, maximum);
}

static void load_tileset_images(preview_app* app)
{
    size_t i;

    app->image_count = app->map != NULL ? app->map->tileset_count : 0;
    if (app->image_count == 0)
    {
        return;
    }

    app->images = (preview_tileset_image*)calloc(app->image_count, sizeof(preview_tileset_image));
    if (app->images == NULL)
    {
        app->image_count = 0;
        return;
    }

    for (i = 0; i < app->image_count; i++)
    {
        BITMAP bitmap;
        preview_tileset_image* image = &app->images[i];
        image->tileset = &app->map->tilesets[i];
        image->resolved_path = resolve_image_path(app, image->tileset->image);
        image->has_transparent = parse_hex_color(image->tileset->transparent_color, &image->transparent_color);

        if (image->resolved_path == NULL)
        {
            continue;
        }

        image->bitmap = (HBITMAP)LoadImageW(
            NULL,
            image->resolved_path,
            IMAGE_BITMAP,
            0,
            0,
            LR_LOADFROMFILE | LR_CREATEDIBSECTION);
        if (image->bitmap == NULL)
        {
            continue;
        }

        memset(&bitmap, 0, sizeof(bitmap));
        if (GetObjectW(image->bitmap, sizeof(bitmap), &bitmap) == 0)
        {
            DeleteObject(image->bitmap);
            image->bitmap = NULL;
            continue;
        }

        image->width = bitmap.bmWidth;
        image->height = bitmap.bmHeight;
        image->columns = image->tileset->tile_size > 0 ? image->width / image->tileset->tile_size : 0;
        image->rows = image->tileset->tile_size > 0 ? image->height / image->tileset->tile_size : 0;
    }
}

static void draw_fallback_tile(HDC hdc, const RECT* destination, const gemap_tile* tile, gemap_tileset_kind kind)
{
    HBRUSH brush = CreateSolidBrush(kind == GEMAP_TILESET_ADVANCED ? RGB(92, 92, 116) : RGB(88, 108, 98));
    wchar_t label[32];

    FillRect(hdc, destination, brush);
    DeleteObject(brush);

    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, RGB(235, 238, 244));
    swprintf_s(label, sizeof(label) / sizeof(label[0]), L"%d", tile->tile_id);
    DrawTextW(hdc, label, -1, (RECT*)destination, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
}

static void draw_tile_image(HDC hdc, const preview_app* app, const gemap_tile* tile, const RECT* destination)
{
    const preview_tileset_image* image = find_image_by_tileset_id(app, tile->tile_set_id);
    int source_size;
    int source_x;
    int source_y;
    HDC source_dc;
    HGDIOBJ old_bitmap;

    if (image == NULL || image->bitmap == NULL || image->columns <= 0)
    {
        draw_fallback_tile(hdc, destination, tile, GEMAP_TILESET_UNKNOWN);
        return;
    }

    if (tile->tile_id < 0 || tile->tile_id >= image->columns * max_int(1, image->rows))
    {
        draw_fallback_tile(hdc, destination, tile, image->tileset->kind);
        return;
    }

    source_size = image->tileset->tile_size;
    source_x = (tile->tile_id % image->columns) * source_size;
    source_y = (tile->tile_id / image->columns) * source_size;
    source_dc = CreateCompatibleDC(hdc);
    old_bitmap = SelectObject(source_dc, image->bitmap);

    SetStretchBltMode(hdc, COLORONCOLOR);
    if (image->has_transparent)
    {
        TransparentBlt(
            hdc,
            destination->left,
            destination->top,
            destination->right - destination->left,
            destination->bottom - destination->top,
            source_dc,
            source_x,
            source_y,
            source_size,
            source_size,
            image->transparent_color);
    }
    else
    {
        StretchBlt(
            hdc,
            destination->left,
            destination->top,
            destination->right - destination->left,
            destination->bottom - destination->top,
            source_dc,
            source_x,
            source_y,
            source_size,
            source_size,
            SRCCOPY);
    }

    SelectObject(source_dc, old_bitmap);
    DeleteDC(source_dc);
}

static int tile_destination_rect(const preview_app* app, const gemap_tile* tile, RECT* destination)
{
    RECT view = map_view_rect(app);
    int size = app->map->tile_size;
    destination->left = view.left + tile->x * size - app->scroll_x;
    destination->top = view.top + tile->y * size - app->scroll_y;
    destination->right = destination->left + size;
    destination->bottom = destination->top + size;
    return destination->right > view.left
        && destination->bottom > view.top
        && destination->left < view.right
        && destination->top < view.bottom;
}

static void draw_layer(HDC hdc, const preview_app* app, gemap_tileset_kind kind)
{
    const gemap_layer* layer = find_layer_by_kind(app->map, kind);
    size_t i;

    if (layer == NULL || !layer->visible)
    {
        return;
    }

    for (i = 0; i < layer->tile_count; i++)
    {
        RECT destination;
        if (tile_destination_rect(app, &layer->tiles[i], &destination))
        {
            draw_tile_image(hdc, app, &layer->tiles[i], &destination);
        }
    }
}

static void draw_grid(HDC hdc, const preview_app* app)
{
    RECT view = map_view_rect(app);
    int size;
    int x;
    int y;
    HPEN pen;
    HGDIOBJ old_pen;

    if (!app->show_grid || app->map == NULL)
    {
        return;
    }

    size = app->map->tile_size;
    pen = CreatePen(PS_SOLID, 1, RGB(78, 84, 94));
    old_pen = SelectObject(hdc, pen);

    for (x = view.left - (app->scroll_x % size); x <= view.right; x += size)
    {
        MoveToEx(hdc, x, view.top, NULL);
        LineTo(hdc, x, view.bottom);
    }

    for (y = view.top - (app->scroll_y % size); y <= view.bottom; y += size)
    {
        MoveToEx(hdc, view.left, y, NULL);
        LineTo(hdc, view.right, y);
    }

    SelectObject(hdc, old_pen);
    DeleteObject(pen);
}

static void draw_dim(HDC hdc, const preview_app* app, const RECT* destination)
{
    BLENDFUNCTION blend;
    blend.BlendOp = AC_SRC_OVER;
    blend.BlendFlags = 0;
    blend.SourceConstantAlpha = 138;
    blend.AlphaFormat = 0;

    AlphaBlend(
        hdc,
        destination->left,
        destination->top,
        destination->right - destination->left,
        destination->bottom - destination->top,
        app->dim_dc,
        0,
        0,
        1,
        1,
        blend);
}

static void draw_center_label(HDC hdc, const preview_app* app, const RECT* destination, const wchar_t* label)
{
    RECT text_rect = *destination;
    RECT shadow_rect;
    HGDIOBJ old_font;

    if (label == NULL || label[0] == L'\0')
    {
        return;
    }

    InflateRect(&text_rect, -2, -2);
    shadow_rect = text_rect;
    OffsetRect(&shadow_rect, 1, 1);
    old_font = SelectObject(hdc, app->label_font);
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, RGB(0, 0, 0));
    DrawTextW(hdc, label, -1, &shadow_rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
    SetTextColor(hdc, RGB(255, 255, 255));
    DrawTextW(hdc, label, -1, &text_rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
    SelectObject(hdc, old_font);
}

static void draw_overlay(HDC hdc, const preview_app* app)
{
    const gemap_layer* layer;
    const gemap_attribute_list* list;
    size_t i;

    if (app->mode == PREVIEW_MODE_NORMAL)
    {
        return;
    }

    layer = find_layer_by_kind(app->map, app->overlay_kind);
    if (layer == NULL)
    {
        return;
    }

    list = find_attribute_list(app->map, app->map->active_attribute_list_id);
    for (i = 0; i < layer->tile_count; i++)
    {
        RECT destination;
        wchar_t label[96];
        const gemap_tile* tile = &layer->tiles[i];

        if (!tile_destination_rect(app, tile, &destination))
        {
            continue;
        }

        draw_dim(hdc, app, &destination);
        if (app->mode == PREVIEW_MODE_ATTRIBUTES)
        {
            format_attribute_label(list, tile->attribute_values, tile->attribute_value_count, label, sizeof(label) / sizeof(label[0]));
            draw_center_label(hdc, app, &destination, label);
        }
        else
        {
            swprintf_s(label, sizeof(label) / sizeof(label[0]), L"%d", tile->display_priority);
            draw_center_label(hdc, app, &destination, label);
        }
    }
}

static void paint_map(preview_app* app, HDC paint_dc)
{
    RECT view = map_view_rect(app);
    int width = max_int(1, view.right - view.left);
    int height = max_int(1, view.bottom - view.top);
    HDC buffer_dc = CreateCompatibleDC(paint_dc);
    HBITMAP buffer = CreateCompatibleBitmap(paint_dc, width, height);
    HGDIOBJ old_buffer = SelectObject(buffer_dc, buffer);
    RECT local_view;
    POINT old_origin;
    HBRUSH background = CreateSolidBrush(RGB(44, 46, 50));

    local_view.left = 0;
    local_view.top = 0;
    local_view.right = width;
    local_view.bottom = height;
    FillRect(buffer_dc, &local_view, background);
    DeleteObject(background);

    SetViewportOrgEx(buffer_dc, -view.left, -view.top, &old_origin);
    draw_layer(buffer_dc, app, GEMAP_TILESET_BASE);
    draw_layer(buffer_dc, app, GEMAP_TILESET_ADVANCED);
    draw_overlay(buffer_dc, app);
    draw_grid(buffer_dc, app);
    SetViewportOrgEx(buffer_dc, old_origin.x, old_origin.y, NULL);

    BitBlt(paint_dc, view.left, view.top, width, height, buffer_dc, 0, 0, SRCCOPY);
    SelectObject(buffer_dc, old_buffer);
    DeleteObject(buffer);
    DeleteDC(buffer_dc);
}

static void update_status_for_cell(preview_app* app, int pixel_x, int pixel_y)
{
    RECT view = map_view_rect(app);
    int cell_x;
    int cell_y;
    gemap_tileset_kind kind = app->overlay_kind;
    const gemap_layer* layer;
    const gemap_tile* tile;
    const gemap_attribute_list* list;
    wchar_t attrs[128];
    wchar_t display[96];
    wchar_t tile_set[160];
    wchar_t status[512];

    if (pixel_x < view.left || pixel_x >= view.right || pixel_y < view.top || pixel_y >= view.bottom)
    {
        SetWindowTextW(app->status, L"Ready");
        return;
    }

    cell_x = (pixel_x - view.left + app->scroll_x) / app->map->tile_size;
    cell_y = (pixel_y - view.top + app->scroll_y) / app->map->tile_size;
    if (cell_x < 0 || cell_y < 0 || cell_x >= app->map->width || cell_y >= app->map->height)
    {
        SetWindowTextW(app->status, L"Ready");
        return;
    }

    if (app->mode == PREVIEW_MODE_NORMAL)
    {
        tile = find_top_tile_at(app, cell_x, cell_y, &kind);
    }
    else
    {
        layer = find_layer_by_kind(app->map, kind);
        tile = gemap_layer_find_tile(layer, cell_x, cell_y);
    }

    if (tile == NULL)
    {
        swprintf_s(status, sizeof(status) / sizeof(status[0]), L"x=%d y=%d empty", cell_x, cell_y);
        SetWindowTextW(app->status, status);
        return;
    }

    list = find_attribute_list(app->map, app->map->active_attribute_list_id);
    format_int_list(tile->attribute_values, tile->attribute_value_count, attrs, sizeof(attrs) / sizeof(attrs[0]));
    format_attribute_label(list, tile->attribute_values, tile->attribute_value_count, display, sizeof(display) / sizeof(display[0]));
    tile_set[0] = L'\0';
    append_utf8_text(tile_set, sizeof(tile_set) / sizeof(tile_set[0]), tile->tile_set_id);
    swprintf_s(
        status,
        sizeof(status) / sizeof(status[0]),
        L"x=%d y=%d layer=%s tileSet=%s tile=%d attrs=%s display=%s priority=%d",
        cell_x,
        cell_y,
        kind_label(kind),
        tile_set,
        tile->tile_id,
        attrs,
        display[0] != L'\0' ? display : L"-",
        tile->display_priority);
    SetWindowTextW(app->status, status);
}

static void layout_controls(preview_app* app)
{
    int y = CONTROL_MARGIN;
    int label_width = 48;
    int combo_width = 140;
    int x = CONTROL_MARGIN;
    HWND label;

    label = GetDlgItem(app->window, 2001);
    if (label != NULL)
    {
        MoveWindow(label, x, y + 4, label_width, CONTROL_HEIGHT, TRUE);
    }
    x += label_width;
    MoveWindow(app->mode_combo, x, y, combo_width, CONTROL_HEIGHT + 120, TRUE);
    x += combo_width + CONTROL_MARGIN;

    label = GetDlgItem(app->window, 2002);
    if (label != NULL)
    {
        MoveWindow(label, x, y + 4, label_width, CONTROL_HEIGHT, TRUE);
    }
    x += label_width;
    MoveWindow(app->layer_combo, x, y, 120, CONTROL_HEIGHT + 80, TRUE);
    x += 120 + CONTROL_MARGIN;
    MoveWindow(app->grid_check, x, y + 2, 90, CONTROL_HEIGHT, TRUE);
    MoveWindow(app->status, 0, max_int(0, app->client_height - STATUS_HEIGHT), app->client_width, STATUS_HEIGHT, TRUE);
    update_scroll_bars(app);
}

static void create_controls(preview_app* app)
{
    app->ui_font = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
    app->mode_combo = CreateWindowExW(
        0,
        L"COMBOBOX",
        NULL,
        WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_TABSTOP,
        0,
        0,
        0,
        0,
        app->window,
        (HMENU)(INT_PTR)ID_MODE_COMBO,
        app->instance,
        NULL);
    SendMessageW(app->mode_combo, WM_SETFONT, (WPARAM)app->ui_font, TRUE);
    SendMessageW(app->mode_combo, CB_ADDSTRING, 0, (LPARAM)L"通常");
    SendMessageW(app->mode_combo, CB_ADDSTRING, 0, (LPARAM)L"属性");
    SendMessageW(app->mode_combo, CB_ADDSTRING, 0, (LPARAM)L"表示優先度");
    SendMessageW(app->mode_combo, CB_SETCURSEL, 0, 0);

    app->layer_combo = CreateWindowExW(
        0,
        L"COMBOBOX",
        NULL,
        WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_TABSTOP,
        0,
        0,
        0,
        0,
        app->window,
        (HMENU)(INT_PTR)ID_LAYER_COMBO,
        app->instance,
        NULL);
    SendMessageW(app->layer_combo, WM_SETFONT, (WPARAM)app->ui_font, TRUE);
    SendMessageW(app->layer_combo, CB_ADDSTRING, 0, (LPARAM)L"ベース");
    SendMessageW(app->layer_combo, CB_ADDSTRING, 0, (LPARAM)L"アドバンス");
    SendMessageW(app->layer_combo, CB_SETCURSEL, 0, 0);

    app->grid_check = CreateWindowExW(
        0,
        L"BUTTON",
        L"グリッド",
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX | WS_TABSTOP,
        0,
        0,
        0,
        0,
        app->window,
        (HMENU)(INT_PTR)ID_GRID_CHECK,
        app->instance,
        NULL);
    SendMessageW(app->grid_check, WM_SETFONT, (WPARAM)app->ui_font, TRUE);
    SendMessageW(app->grid_check, BM_SETCHECK, BST_CHECKED, 0);

    CreateWindowExW(
        0,
        L"STATIC",
        L"表示:",
        WS_CHILD | WS_VISIBLE,
        0,
        0,
        0,
        0,
        app->window,
        (HMENU)(INT_PTR)2001,
        app->instance,
        NULL);
    CreateWindowExW(
        0,
        L"STATIC",
        L"対象:",
        WS_CHILD | WS_VISIBLE,
        0,
        0,
        0,
        0,
        app->window,
        (HMENU)(INT_PTR)2002,
        app->instance,
        NULL);

    app->status = CreateWindowExW(
        WS_EX_CLIENTEDGE,
        L"STATIC",
        L"Ready",
        WS_CHILD | WS_VISIBLE | SS_LEFTNOWORDWRAP,
        0,
        0,
        0,
        0,
        app->window,
        NULL,
        app->instance,
        NULL);
    SendMessageW(app->status, WM_SETFONT, (WPARAM)app->ui_font, TRUE);
}

static void create_drawing_resources(preview_app* app)
{
    HDC screen = GetDC(NULL);
    int label_height = -max_int(10, min_int(18, app->map != NULL ? app->map->tile_size / 2 : 14));
    app->label_font = CreateFontW(
        label_height,
        0,
        0,
        0,
        FW_BOLD,
        FALSE,
        FALSE,
        FALSE,
        DEFAULT_CHARSET,
        OUT_DEFAULT_PRECIS,
        CLIP_DEFAULT_PRECIS,
        DEFAULT_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE,
        L"Segoe UI");
    app->dim_dc = CreateCompatibleDC(screen);
    app->dim_bitmap = CreateCompatibleBitmap(screen, 1, 1);
    app->old_dim_bitmap = (HBITMAP)SelectObject(app->dim_dc, app->dim_bitmap);
    SetPixel(app->dim_dc, 0, 0, RGB(0, 0, 0));
    ReleaseDC(NULL, screen);
}

static void destroy_app(preview_app* app)
{
    size_t i;

    if (app == NULL)
    {
        return;
    }

    if (app->dim_dc != NULL)
    {
        SelectObject(app->dim_dc, app->old_dim_bitmap);
        DeleteDC(app->dim_dc);
    }
    if (app->dim_bitmap != NULL)
    {
        DeleteObject(app->dim_bitmap);
    }
    if (app->label_font != NULL)
    {
        DeleteObject(app->label_font);
    }

    for (i = 0; i < app->image_count; i++)
    {
        if (app->images[i].bitmap != NULL)
        {
            DeleteObject(app->images[i].bitmap);
        }
        free(app->images[i].resolved_path);
    }
    free(app->images);
    gemap_free(app->map);
    free(app->map_path);
    free(app->map_dir);
    free(app->asset_root);
    free(app->exe_dir);
}

static void update_mode_from_controls(preview_app* app)
{
    LRESULT mode = SendMessageW(app->mode_combo, CB_GETCURSEL, 0, 0);
    LRESULT layer = SendMessageW(app->layer_combo, CB_GETCURSEL, 0, 0);
    app->mode = mode == 1 ? PREVIEW_MODE_ATTRIBUTES : mode == 2 ? PREVIEW_MODE_PRIORITY : PREVIEW_MODE_NORMAL;
    app->overlay_kind = layer == 1 ? GEMAP_TILESET_ADVANCED : GEMAP_TILESET_BASE;
    app->show_grid = SendMessageW(app->grid_check, BM_GETCHECK, 0, 0) == BST_CHECKED;
    InvalidateRect(app->window, NULL, FALSE);
}

static LRESULT CALLBACK preview_window_proc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    preview_app* app = (preview_app*)GetWindowLongPtrW(hwnd, GWLP_USERDATA);

    if (message == WM_NCCREATE)
    {
        CREATESTRUCTW* create = (CREATESTRUCTW*)lParam;
        app = (preview_app*)create->lpCreateParams;
        app->window = hwnd;
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, (LONG_PTR)app);
    }

    switch (message)
    {
    case WM_CREATE:
        create_controls(app);
        create_drawing_resources(app);
        return 0;
    case WM_SIZE:
        app->client_width = LOWORD(lParam);
        app->client_height = HIWORD(lParam);
        layout_controls(app);
        return 0;
    case WM_COMMAND:
        if ((LOWORD(wParam) == ID_MODE_COMBO && HIWORD(wParam) == CBN_SELCHANGE)
            || (LOWORD(wParam) == ID_LAYER_COMBO && HIWORD(wParam) == CBN_SELCHANGE)
            || (LOWORD(wParam) == ID_GRID_CHECK && HIWORD(wParam) == BN_CLICKED))
        {
            update_mode_from_controls(app);
            return 0;
        }
        break;
    case WM_HSCROLL:
    {
        RECT view = map_view_rect(app);
        int page = max_int(1, view.right - view.left);
        int maximum = max_int(0, map_pixel_width(app) - page);
        scroll_window_to(app, scroll_from_request(hwnd, SB_HORZ, wParam, app->scroll_x, page, maximum), app->scroll_y);
        return 0;
    }
    case WM_VSCROLL:
    {
        RECT view = map_view_rect(app);
        int page = max_int(1, view.bottom - view.top);
        int maximum = max_int(0, map_pixel_height(app) - page);
        scroll_window_to(app, app->scroll_x, scroll_from_request(hwnd, SB_VERT, wParam, app->scroll_y, page, maximum));
        return 0;
    }
    case WM_MOUSEWHEEL:
        scroll_window_to(app, app->scroll_x, app->scroll_y - GET_WHEEL_DELTA_WPARAM(wParam) / WHEEL_DELTA * app->map->tile_size * 3);
        return 0;
    case WM_MOUSEMOVE:
        update_status_for_cell(app, GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
        return 0;
    case WM_KEYDOWN:
        if (wParam == VK_ESCAPE)
        {
            DestroyWindow(hwnd);
            return 0;
        }
        break;
    case WM_PAINT:
    {
        PAINTSTRUCT paint;
        HDC hdc = BeginPaint(hwnd, &paint);
        RECT toolbar;
        HBRUSH toolbar_brush = CreateSolidBrush(RGB(245, 246, 248));
        toolbar.left = 0;
        toolbar.top = 0;
        toolbar.right = app->client_width;
        toolbar.bottom = TOOLBAR_HEIGHT;
        FillRect(hdc, &toolbar, toolbar_brush);
        DeleteObject(toolbar_brush);
        paint_map(app, hdc);
        EndPaint(hwnd, &paint);
        return 0;
    }
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}

static int parse_command_line(preview_app* app, int argc, wchar_t** argv, int* validate_only)
{
    int i;

    *validate_only = 0;
    for (i = 1; i < argc; i++)
    {
        if (wcscmp(argv[i], L"--asset-root") == 0 && i + 1 < argc)
        {
            free(app->asset_root);
            app->asset_root = duplicate_wide(argv[++i]);
        }
        else if (wcscmp(argv[i], L"--validate") == 0)
        {
            *validate_only = 1;
        }
        else if (wcscmp(argv[i], L"--no-wait") == 0)
        {
            continue;
        }
        else
        {
            free(app->map_path);
            app->map_path = duplicate_wide(argv[i]);
        }
    }

    return app->map_path != NULL;
}

static int any_image_missing(const preview_app* app)
{
    size_t i;

    for (i = 0; i < app->image_count; i++)
    {
        if (app->images[i].bitmap == NULL)
        {
            return 1;
        }
    }

    return 0;
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous_instance, PWSTR command_line, int show_command)
{
    preview_app app;
    int argc = 0;
    int validate_only = 0;
    wchar_t** argv;
    gemap_error error;
    gemap_result result;
    wchar_t* map_name;
    wchar_t title[512];
    WNDCLASSW window_class;
    HWND window;
    MSG message;

    (void)previous_instance;
    (void)command_line;

    memset(&app, 0, sizeof(app));
    app.instance = instance;
    app.overlay_kind = GEMAP_TILESET_BASE;
    app.show_grid = 1;
    app.exe_dir = get_executable_directory();
    app.asset_root = duplicate_wide(L"");

    argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv == NULL || !parse_command_line(&app, argc, argv, &validate_only))
    {
        MessageBoxW(NULL, L"Usage: GameEditor.MapPreview.exe [--asset-root path] [--validate] <map.gemap.json>", L"gameEditor Map Preview", MB_OK | MB_ICONINFORMATION);
        if (argv != NULL)
        {
            LocalFree(argv);
        }
        destroy_app(&app);
        return 2;
    }
    LocalFree(argv);

    app.map_dir = get_directory_name(app.map_path);
    memset(&error, 0, sizeof(error));
    result = load_map_file_w(app.map_path, &app.map, &error);
    if (result != GEMAP_OK)
    {
        wchar_t* message_text = utf8_to_wide(error.message[0] != '\0' ? error.message : gemap_result_name(result));
        MessageBoxW(NULL, message_text != NULL ? message_text : L"Failed to load map.", L"Map load error", MB_OK | MB_ICONERROR);
        free(message_text);
        destroy_app(&app);
        return 1;
    }

    load_tileset_images(&app);
    if (validate_only)
    {
        int missing = any_image_missing(&app);
        destroy_app(&app);
        return missing ? 3 : 0;
    }

    map_name = utf8_to_wide(app.map->name);
    swprintf_s(title, sizeof(title) / sizeof(title[0]), L"gameEditor Map Preview - %s", map_name != NULL ? map_name : L"Untitled");
    free(map_name);

    memset(&window_class, 0, sizeof(window_class));
    window_class.lpfnWndProc = preview_window_proc;
    window_class.hInstance = instance;
    window_class.lpszClassName = L"GameEditorMapPreviewWindow";
    window_class.hCursor = LoadCursorW(NULL, IDC_ARROW);
    window_class.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    if (!RegisterClassW(&window_class))
    {
        MessageBoxW(NULL, L"Failed to register preview window.", L"Preview error", MB_OK | MB_ICONERROR);
        destroy_app(&app);
        return 1;
    }

    window = CreateWindowExW(
        0,
        window_class.lpszClassName,
        title,
        WS_OVERLAPPEDWINDOW | WS_HSCROLL | WS_VSCROLL,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        980,
        720,
        NULL,
        NULL,
        instance,
        &app);
    if (window == NULL)
    {
        MessageBoxW(NULL, L"Failed to create preview window.", L"Preview error", MB_OK | MB_ICONERROR);
        destroy_app(&app);
        return 1;
    }

    ShowWindow(window, show_command);
    UpdateWindow(window);

    while (GetMessageW(&message, NULL, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    destroy_app(&app);
    return (int)message.wParam;
}

#else

#include <stdio.h>

int main(void)
{
    fprintf(stderr, "GameEditor.MapPreview requires Windows.\n");
    return 1;
}

#endif
