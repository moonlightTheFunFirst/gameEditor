# GameEditor.MapRuntime

`GameEditor.MapRuntime` is a small C ABI library for loading `.gemap.json` files exported by gameEditor.
The first preview executable is intentionally simple: it loads a map, prints the parsed data, and renders each layer as a compact text grid.

## Build

```powershell
cmake -S src/GameEditor.MapRuntime -B Output/MapRuntime
cmake --build Output/MapRuntime --config Release
```

Or run:

```powershell
.\build_map_preview.bat
```

The preview executable is generated as:

```text
Output/MapRuntime/Release/GameEditor.MapPreview.exe
```

Single-configuration generators may place it directly under `Output/MapRuntime`.

## Run

```powershell
Output/MapRuntime/Release/GameEditor.MapPreview.exe --no-wait testData/NewMap.gemap.json
```

The WinForms editor looks for `GameEditor.MapPreview.exe` near the application output and under `Output/MapRuntime`.
