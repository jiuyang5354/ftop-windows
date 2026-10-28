**中文** | [English](README.en.md)

# Ftop for Windows（原型）

把 [ftop](https://github.com/Nongfsq/ftop)（Nongfsq，MIT）的设计搬到 Windows 的尝试：
一块悬浮的玻璃拟态面板，任意拖拽缩放，静悄悄地显示这台机器在忙什么。

> 原型状态：功能可用，但只在作者的 Linux 机器上写完、**没有在 Windows 上编译验证过**。
> 在 Windows 上用 VS 2022 打开 `Ftop.sln` 编译运行，遇到问题欢迎提。

## 长什么样

- 无边框悬浮窗，亚克力模糊背景，圆角 14px，点击不抢焦点
- 顶部细条：拖拽移动，◉ 置顶开关，✕ 关闭；八向边缘拖拽缩放
- 右键菜单：深色/浅色切换、动效开关、打开配置文件、退出

## 四档自适应布局（抄 ftop 的 Ladder 思想）

| 尺寸 | 内容 |
|---|---|
| XS（~150px） | CPU 平均占用一行 |
| S（~230px） | 每核心柱状 + 内存 + 网络 |
| M（~320px） | 再加 GPU + 功耗 |
| L（~430px） | 再加 Top 进程列表（带图标，随窗口长高增减行数） |

## 动效

- 核心柱状、仪表读数每帧指数缓动趋近真实值，不是一秒一跳
- 进程列表"静置"：只有榜首易主或 2.5 秒后才重排，两行差 1% 不乱跳
- 系统开"减少动态效果"或配置里 `reducedMotion` 时直接吸附

## 数据来源（Windows）

| 指标 | 来源 |
|---|---|
| 每核心占用 | PerformanceCounter `Processor/% Processor Time` |
| 每核心频率 | `Processor/% Processor Performance` × 注册表 `~MHz`（近似值） |
| 内存 | `GlobalMemoryStatusEx`（压力值用系统自带的 MemoryLoad） |
| 网络 | `Network Interface/Bytes Received/sec` 汇总（排除回环） |
| GPU | `GPU Engine/Utilization Percentage` 按物理显卡汇总 |
| 进程 | `TotalProcessorTime` 差分 / 机器总核数（和 ftop 一样，永远不超过 100%） |
| 功耗 / 温度 | Windows 没有通用的免提权 API，**如实显示"—"**，不编数字 |

采样 1Hz；窗口最小化时暂停采样。单实例运行。

## 配置

`%APPDATA%\Ftop\config.json`（首次运行自动生成），改完实时生效：

```json
{
  "moduleOrder": ["cores", "memory", "network", "gpu", "power", "processes"],
  "palette": "dark",
  "refreshMs": 1000,
  "reducedMotion": false,
  "showUnavailable": true
}
```

## 构建

- VS 2022 17.8+，.NET 8 SDK
- 打开 `Ftop.sln`，选 x64 → 生成 → 运行
- 单文件发布：`dotnet publish src/Ftop.App -c Release -r win-x64 --self-contained`

## 架构

```
Ftop.Core     领域模型：Snapshot（不可变）、Reading（值或不可用）、
              LayoutLadder（纯函数梯子）、FtopConfig、Format
Ftop.Sensors  数据采集：各 Provider + Sampler（1Hz 定时器产出 Snapshot）
Ftop.App      WPF：亚克力悬浮窗、自绘模块视图、缓动引擎、梯子布局
```

UI 层只消费 `Snapshot`，Provider 抛出的异常永远到不了界面——
对应读数降级为"不可用"，面板不空白。

## 致谢

设计、交互与架构思想来自 [Nongfsq/ftop](https://github.com/Nongfsq/ftop)（MIT License），
详见 [NOTICE.md](NOTICE.md)。本项目为学习性质的移植尝试，与原作者无关。
