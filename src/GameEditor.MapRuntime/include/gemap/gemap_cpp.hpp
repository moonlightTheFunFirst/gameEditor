#ifndef GEMAP_GEMAP_CPP_HPP
#define GEMAP_GEMAP_CPP_HPP

#include "gemap.h"

#include <stdexcept>
#include <string>

namespace gemap
{
class Map
{
public:
    Map() = default;

    explicit Map(gemap_map* map) noexcept
        : map_(map)
    {
    }

    Map(const Map&) = delete;
    Map& operator=(const Map&) = delete;

    Map(Map&& other) noexcept
        : map_(other.map_)
    {
        other.map_ = nullptr;
    }

    Map& operator=(Map&& other) noexcept
    {
        if (this != &other)
        {
            gemap_free(map_);
            map_ = other.map_;
            other.map_ = nullptr;
        }

        return *this;
    }

    ~Map()
    {
        gemap_free(map_);
    }

    static Map load_file(const char* path)
    {
        gemap_error error{};
        gemap_map* map = nullptr;
        const auto result = gemap_load_file(path, &map, &error);
        if (result != GEMAP_OK)
        {
            throw std::runtime_error(error.message[0] != '\0' ? error.message : gemap_result_name(result));
        }

        return Map(map);
    }

    const gemap_map* get() const noexcept
    {
        return map_;
    }

    const gemap_map& operator*() const noexcept
    {
        return *map_;
    }

    const gemap_map* operator->() const noexcept
    {
        return map_;
    }

private:
    gemap_map* map_ = nullptr;
};
}

#endif
