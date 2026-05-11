#include "gemap/gemap.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#endif

static const char* safe_text(const char* value)
{
    return value != NULL ? value : "";
}

static char tile_symbol(int tile_id)
{
    static const char symbols[] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    if (tile_id >= 0 && tile_id < (int)(sizeof(symbols) - 1))
    {
        return symbols[tile_id];
    }

    return '+';
}

static void print_int_list(const int* values, size_t count)
{
    size_t i;

    printf("[");
    for (i = 0; i < count; i++)
    {
        if (i > 0)
        {
            printf(", ");
        }
        printf("%d", values[i]);
    }
    printf("]");
}

static void print_attributes(const gemap_map* map)
{
    size_t i;

    if (map->attribute_list_count == 0)
    {
        printf("Attribute lists: none\n");
        return;
    }

    printf("Attribute lists:\n");
    for (i = 0; i < map->attribute_list_count; i++)
    {
        size_t j;
        const gemap_attribute_list* list = &map->attribute_lists[i];
        printf("  %s (%s)\n", safe_text(list->name), safe_text(list->id));
        for (j = 0; j < list->value_count; j++)
        {
            const gemap_attribute_value* value = &list->values[j];
            printf(
                "    %d: %s display=%s memo=%s\n",
                value->value,
                safe_text(value->name),
                safe_text(value->display_text),
                safe_text(value->memo));
        }
    }
}

static void print_tilesets(const gemap_map* map)
{
    size_t i;

    if (map->tileset_count == 0)
    {
        printf("Tile sets: none\n");
        return;
    }

    printf("Tile sets:\n");
    for (i = 0; i < map->tileset_count; i++)
    {
        const gemap_tileset* tileset = &map->tilesets[i];
        printf(
            "  %s (%s) kind=%s tileSize=%d image=%s attrs=%zu priorities=%zu\n",
            safe_text(tileset->name),
            safe_text(tileset->id),
            gemap_tileset_kind_name(tileset->kind),
            tileset->tile_size,
            safe_text(tileset->image),
            tileset->tile_attribute_count,
            tileset->tile_priority_count);
    }
}

static void print_layer_grid(const gemap_map* map, const gemap_layer* layer)
{
    int x;
    int y;
    int width = map->width < 64 ? map->width : 64;
    int height = map->height < 32 ? map->height : 32;

    printf("    grid preview");
    if (width != map->width || height != map->height)
    {
        printf(" (truncated to %dx%d)", width, height);
    }
    printf(":\n");

    for (y = 0; y < height; y++)
    {
        printf("    ");
        for (x = 0; x < width; x++)
        {
            const gemap_tile* tile = gemap_layer_find_tile(layer, x, y);
            putchar(tile != NULL ? tile_symbol(tile->tile_id) : '.');
        }
        putchar('\n');
    }
}

static void print_layer_details(const gemap_layer* layer)
{
    size_t i;
    size_t limit = layer->tile_count < 8 ? layer->tile_count : 8;

    if (layer->tile_count == 0)
    {
        return;
    }

    printf("    first tiles:\n");
    for (i = 0; i < limit; i++)
    {
        const gemap_tile* tile = &layer->tiles[i];
        printf(
            "      (%d,%d) tileSet=%s tile=%d attrs=",
            tile->x,
            tile->y,
            safe_text(tile->tile_set_id),
            tile->tile_id);
        print_int_list(tile->attribute_values, tile->attribute_value_count);
        printf(" priority=%d\n", tile->display_priority);
    }

    if (layer->tile_count > limit)
    {
        printf("      ... %zu more tiles\n", layer->tile_count - limit);
    }
}

static void print_layers(const gemap_map* map)
{
    size_t i;

    if (map->layer_count == 0)
    {
        printf("Layers: none\n");
        return;
    }

    printf("Layers:\n");
    for (i = 0; i < map->layer_count; i++)
    {
        const gemap_layer* layer = &map->layers[i];
        printf(
            "  %s (%s) kind=%s visible=%d locked=%d z=%d tiles=%zu\n",
            safe_text(layer->name),
            safe_text(layer->id),
            gemap_tileset_kind_name(layer->kind),
            layer->visible,
            layer->locked,
            layer->z_index,
            layer->tile_count);
        print_layer_grid(map, layer);
        print_layer_details(layer);
    }
}

static void wait_to_close(int wait_enabled)
{
    if (!wait_enabled)
    {
        return;
    }

    printf("\nPress Enter to close...");
    fflush(stdout);
    (void)getchar();
}

static void print_usage(const char* executable)
{
    printf("Usage: %s [--no-wait] <map.gemap.json>\n", executable);
}

int main(int argc, char** argv)
{
    const char* path = NULL;
    int wait_enabled = 0;
    int i;
    gemap_map* map = NULL;
    gemap_error error;
    gemap_result result;

#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
    wait_enabled = 1;
#endif

    for (i = 1; i < argc; i++)
    {
        if (strcmp(argv[i], "--no-wait") == 0)
        {
            wait_enabled = 0;
        }
        else
        {
            path = argv[i];
        }
    }

    if (path == NULL)
    {
        print_usage(argv[0]);
        wait_to_close(wait_enabled);
        return 2;
    }

    result = gemap_load_file(path, &map, &error);
    if (result != GEMAP_OK)
    {
        fprintf(stderr, "Failed to load map: %s\n", error.message[0] != '\0' ? error.message : gemap_result_name(result));
        wait_to_close(wait_enabled);
        return 1;
    }

    printf("gameEditor map preview\n");
    printf("======================\n");
    printf("File: %s\n", path);
    printf("Format: %s schema=%d\n", safe_text(map->format), map->schema_version);
    printf("Map: %s %dx%d tileSize=%d activeAttributeList=%s\n\n",
        safe_text(map->name),
        map->width,
        map->height,
        map->tile_size,
        safe_text(map->active_attribute_list_id));

    print_attributes(map);
    printf("\n");
    print_tilesets(map);
    printf("\n");
    print_layers(map);

    gemap_free(map);
    wait_to_close(wait_enabled);
    return 0;
}
