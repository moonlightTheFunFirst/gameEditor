cmake_minimum_required(VERSION 3.20)

project(GameEditorMapRuntime LANGUAGES C CXX)

set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(gemap STATIC
    src/gemap.c
)

target_include_directories(gemap
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

if(MSVC)
    target_compile_definitions(gemap PRIVATE _CRT_SECURE_NO_WARNINGS)
    target_compile_options(gemap PRIVATE /W4)
else()
    target_compile_options(gemap PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_executable(gemap_preview
    samples/map_preview.c
)

target_link_libraries(gemap_preview PRIVATE gemap)
set_target_properties(gemap_preview PROPERTIES OUTPUT_NAME "GameEditor.MapPreview")

if(MSVC)
    target_compile_options(gemap_preview PRIVATE /W4)
else()
    target_compile_options(gemap_preview PRIVATE -Wall -Wextra -Wpedantic)
endif()
