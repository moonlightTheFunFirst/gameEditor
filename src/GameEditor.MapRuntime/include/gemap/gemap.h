#ifndef GEMAP_GEMAP_H
#define GEMAP_GEMAP_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum gemap_result
{
    GEMAP_OK = 0,
    GEMAP_ERROR_IO,
    GEMAP_ERROR_PARSE,
    GEMAP_ERROR_FORMAT,
    GEMAP_ERROR_MEMORY
} gemap_result;

typedef enum gemap_tileset_kind
{
    GEMAP_TILESET_UNKNOWN = 0,
    GEMAP_TILESET_BASE,
    GEMAP_TILESET_ADVANCED
} gemap_tileset_kind;

typedef struct gemap_error
{
    gemap_result code;
    char message[512];
} gemap_error;

typedef struct gemap_attribute_value
{
    int value;
    char* name;
    char* display_text;
    char* memo;
    char* color;
} gemap_attribute_value;

typedef struct gemap_attribute_list
{
    char* id;
    char* name;
    size_t value_count;
    gemap_attribute_value* values;
} gemap_attribute_list;

typedef struct gemap_tile_attribute_set
{
    int tile_id;
    size_t value_count;
    int* values;
} gemap_tile_attribute_set;

typedef struct gemap_tile_priority
{
    int tile_id;
    int priority;
} gemap_tile_priority;

typedef struct gemap_tileset
{
    char* id;
    char* name;
    gemap_tileset_kind kind;
    char* image;
    int tile_size;
    char* transparent_color;
    char* attribute_list_id;
    size_t tile_attribute_count;
    gemap_tile_attribute_set* tile_attributes;
    size_t tile_priority_count;
    gemap_tile_priority* tile_priorities;
} gemap_tileset;

typedef struct gemap_tile
{
    int x;
    int y;
    char* tile_set_id;
    int tile_id;
    size_t attribute_value_count;
    int* attribute_values;
    int display_priority;
} gemap_tile;

typedef struct gemap_layer
{
    char* id;
    char* name;
    gemap_tileset_kind kind;
    int visible;
    int locked;
    int z_index;
    size_t tile_count;
    gemap_tile* tiles;
} gemap_layer;

typedef struct gemap_map
{
    char* format;
    int schema_version;
    char* name;
    int width;
    int height;
    int tile_size;
    char* active_attribute_list_id;
    size_t attribute_list_count;
    gemap_attribute_list* attribute_lists;
    size_t tileset_count;
    gemap_tileset* tilesets;
    size_t layer_count;
    gemap_layer* layers;
} gemap_map;

gemap_result gemap_load_file(const char* path, gemap_map** out_map, gemap_error* error);
gemap_result gemap_load_json(const char* json, size_t length, gemap_map** out_map, gemap_error* error);
void gemap_free(gemap_map* map);

const char* gemap_result_name(gemap_result result);
const char* gemap_tileset_kind_name(gemap_tileset_kind kind);
const gemap_tileset* gemap_find_tileset(const gemap_map* map, const char* id);
const gemap_layer* gemap_find_layer(const gemap_map* map, const char* id);
const gemap_tile* gemap_layer_find_tile(const gemap_layer* layer, int x, int y);

#ifdef __cplusplus
}
#endif

#endif
