[中文](README.md) | **English**

# Ftop for Windows — prototype

An experimental Windows implementation of the design of [ftop](https://github.com/Nongfsq/ftop) by Nongfsq, licensed under MIT: a floating glass panel that can be dragged and resized to show system activity.

**Prototype status:** the original documentation says it was written on Linux and has not been compiled or verified on Windows. Open `Ftop.sln` in Visual Studio 2022 on Windows to build it.

## Interface

- Borderless acrylic window, 14px corners, and no focus stealing on click.
- Header controls for dragging, always-on-top, and closing; resize from eight edges and corners.
- Context menu for dark/light mode, animation, configuration, and exit.

## Adaptive layout

| Size | Contents |
| --- | --- |
| XS, about 150px | Average CPU usage |
| S, about 230px | Per-core bars, memory, and network |
| M, about 320px | Adds GPU and power |
| L, about 430px | Adds a process list with icons, scaled to window height |

Readings use per-frame exponential smoothing. Process order changes only when the leader changes or after 2.5 seconds; differences under 1% do not reorder the list. Reduced-motion settings disable smoothing.

## Windows data sources

| Metric | Source |
| --- | --- |
| Per-core load | PerformanceCounter `Processor/% Processor Time` |
| Approximate core frequency | `Processor/% Processor Performance` multiplied by registry `~MHz` |
| Memory | `GlobalMemoryStatusEx`; pressure uses the system's MemoryLoad |
| Network | Summed `Network Interface/Bytes Received/sec`, excluding loopback |
| GPU | `GPU Engine/Utilization Percentage`, grouped by physical GPU |
| Processes | `TotalProcessorTime` differences divided by machine core count; never above 100% |
| Power and temperature | Displayed as unavailable (`—`), because no universal unprivileged Windows API is used |

Sampling runs at 1Hz and pauses while minimized. Only one instance runs at a time.

## Configuration

`%APPDATA%\Ftop\config.json` is created on first run. Changes are reloaded:

```json
{
  "moduleOrder": ["cores", "memory", "network", "gpu", "power", "processes"],
  "palette": "dark",
  "refreshMs": 1000,
  "reducedMotion": false,
  "showUnavailable": true
}
```

## Build

Use Visual Studio 2022 17.8+ and the .NET 8 SDK. Open `Ftop.sln`, select x64, and build or run. To publish:

```sh
dotnet publish src/Ftop.App -c Release -r win-x64 --self-contained
```

## Architecture and attribution

- `Ftop.Core`: immutable snapshots, unavailable values, layout ladder, configuration, and formatting.
- `Ftop.Sensors`: providers and a 1Hz sampler.
- `Ftop.App`: WPF, acrylic, custom rendering, animation, and layout.

The UI consumes snapshots; provider failures produce unavailable readings rather than an empty panel.

Design, interaction, and architectural ideas come from [Nongfsq/ftop](https://github.com/Nongfsq/ftop). See [NOTICE.md](NOTICE.md). This is an independent learning experiment, not an official upstream project.
