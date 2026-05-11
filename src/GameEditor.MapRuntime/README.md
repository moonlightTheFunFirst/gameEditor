# GameEditor.MapRuntime

`GameEditor.MapRuntime` is a small C ABI library for loading `.gemap.json` files exported by gameEditor.
`GameEditor.MapPreview.exe` is a Win32/GDI preview viewer that renders the map, attributes, and display priorities from the exported JSON.

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
Output/MapRuntime/Release/GameEditor.MapPreview.exe --asset-root . testData/NewMap.gemap.json
```

For build or CI checks without opening a window:

```powershell
Output/MapRuntime/Release/GameEditor.MapPreview.exe --validate --asset-root . testData/NewMap.gemap.json
```

The WinForms editor looks for `GameEditor.MapPreview.exe` near the application output and under `Output/MapRuntime`.
