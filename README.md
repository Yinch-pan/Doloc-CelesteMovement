# 蔚蓝运动 / Celeste Movement

为 **Doloc Town（多洛可小镇）** 提供受 Celeste 启发的角色运动。作者：Yinch。
当前源码版本：**0.2.10 测试版**；运行前置：**DTMAPI Runtime 0.7.0**。

[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3823554127) · [DTMAPI 前置](https://steamcommunity.com/sharedfiles/filedetails/?id=3743016467) · [DTMAPI 源码与 SDK](https://github.com/yakumo-hx/DTMAPI)

## 功能

- 独立 60 Hz 像素运动核心：可变跳高、离地宽限、输入缓冲、八方向冲刺、快速下落。
- 抓墙、攀爬体力、蹬墙跳、蹲伏、蹲姿冲刺、单向平台下穿。
- 支持 super、hyper、extended hyper、wavedash、wallbounce 等动作。
- 保留多洛可主角和换装，蹲伏与攀爬采用运行时网格动画。
- 冲刺次数决定头发提示色；低攀爬体力时红闪。
- 正常移动固定使用蔚蓝运动；菜单、过场和骑乘等由原版控制。

未获得原版能力时不能冲刺；原版「二段跳」解锁一次蔚蓝冲刺，原版「冲刺」解锁两次。没有空中二段跳。抓墙、攀爬和蹬墙跳从开局可用，可能改变探索顺序。

满次数或未解锁时保留原发色；两次冲刺剩一次时红发，耗尽后蓝发，补充时短暂闪白。

## 安装和操作

玩家优先使用工坊订阅包。先按 DTMAPI 页面说明完成运行环境安装，再订阅、启用本 Mod 并完整重启游戏。建议先复制存档体验。

| 操作 | 默认键位 |
| --- | --- |
| 移动 / 冲刺方向 | WASD 或方向键 |
| 跳跃 / 普通冲刺 | 沿用游戏内设置 |
| 抓墙 | 左 Ctrl；配合上 / 下攀爬 |
| 蹲伏 / 快速下落 | S 或 ↓ |
| 蹲姿冲刺 | C |
| 下穿单向平台 | 按住下，再按跳 |

改键：标题左上角 **DTMAPI → 配置 → 蔚蓝运动**，修改后保存。默认空间比例为 0.50。
更多说明见 [玩家指南](mod/content/PLAYER-GUIDE-zh-CN.txt) 和 [更新记录](mod/content/CHANGELOG-zh-CN.txt)。

## 从源码构建（Windows PowerShell）

1. 准备自己的 Doloc Town 游戏安装和 DTMAPI Runtime 0.7.0。
2. 从 [DTMAPI Releases](https://github.com/yakumo-hx/DTMAPI/releases) 下载 Author SDK 0.7.0，将包含 `dtmapi-author.exe`、`Enter-DtmApiEnvironment.ps1`、`toolchain/` 的目录放到 `.tools/dtmapi-sdk/`。也可以通过 `-SdkRoot` 指定已有 SDK。
3. 在仓库根目录运行，替换示例游戏路径：

```powershell
.\scripts\Build.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\Doloc Town'
# SDK 放在其他位置时：
.\scripts\Build.ps1 -GameRoot 'D:\SteamLibrary\steamapps\common\Doloc Town' -SdkRoot 'D:\Tools\dtmapi-sdk'
```

脚本从 `mod/dtmapi.author.example.json` 创建被 Git 忽略的本地 `mod/dtmapi.author.json`，填入游戏路径，随后运行测试、离线还原、打包和包检查。也支持环境变量 `DTMAPI_GAME_DIR`。以后未传路径时沿用本地配置。

输出：`mod/dist/Yinch.CelesteMovement-0.2.10.zip`。项目目标为 `netstandard2.0`，SDK 使用 .NET 8.0.421。游戏和平台引用只在本机读取，不进入仓库。

本仓库不附带工坊封面图片。源码构建允许省略 `mod/publishing/preview.png` 和 `icon.png`；如要上传自己的工坊条目，请先准备有权使用的图片放在这两个位置后重新构建。现有工坊条目封面来源见 [NOTICE.md](NOTICE.md)。

### 只运行运动测试

安装 .NET 8 SDK 后即可运行，不需要游戏或 DTMAPI：

```powershell
dotnet run --project tests/MovementTests
```

135 项自动检查覆盖运动、碰撞、动画、颜色、能力解锁与场景就绪交接。这些测试不代表所有真实地形均已验证。

### 本地安装

先退出游戏，使用同一游戏路径和 SDK：

```powershell
.\scripts\Manage-Mod.ps1 -Action Deploy    # 首次安装
.\scripts\Manage-Mod.ps1 -Action Update    # 更新已有本地安装
.\scripts\Manage-Mod.ps1 -Action Status
.\scripts\Manage-Mod.ps1 -Action Withdraw  # 撤回本地安装
```

若 SDK 未放在默认位置，同样传入 `-SdkRoot`。从游戏的模组菜单启用后重启。避免同时启用工坊版和本地开发副本。

## 目录

- `src/CelesteMovement.Core/`：不依赖 Unity 的运动与显示状态计算；`MovementMotor` 为保留的旧模型，当前适配使用 `PixelPlayer`。
- `mod/src/`：DTMAPI 入口、Harmony 钩子、Unity 碰撞和动画适配。
- `mod/content/`：玩家说明和更新记录。
- `tests/MovementTests/`：可独立运行的回归检查。
- `scripts/`：构建与本地部署。

## 测试范围与反馈

已在 Windows、Doloc Town 1.00.09、DTMAPI 0.7.0 和键盘环境中测试。手柄、Steam Deck、Linux/macOS 尚未验证；复杂移动地形、挤压及其他角色替换 Mod 仍需测试。不添加 Celeste 专用机关，也不承诺与原作所有场景逐帧一致。

反馈请附版本、地点、复现步骤、同时启用的 Mod 和必要日志片段。提交日志前请移除账号、路径等个人信息。

## 许可与来源

本项目原创代码以 [MIT License](LICENSE) 开源。Celeste 与 Doloc Town 的游戏、素材、商标和第三方组件不在该许可范围内，详见 [NOTICE.md](NOTICE.md)。参考工程、反编译资料、SDK、游戏 DLL、日志、存档和本机配置均不随源码分发。

## English

Celeste-inspired movement for Doloc Town, built on DTMAPI Runtime 0.7.0. Includes variable jumps, eight-direction dashes, climbing stamina, crouching, platform drop-through, advanced movement and outfit-compatible visual feedback. Native double-jump unlock grants one dash; native dash unlock grants two. No air double jump.

Windows/keyboard beta; not a claim of complete frame-for-frame parity. Run independent checks with `dotnet run --project tests/MovementTests`. To build the game mod, supply your own game installation and DTMAPI Author SDK 0.7.0, then run `scripts/Build.ps1 -GameRoot <path> -SdkRoot <path>`. Game assets and third-party binaries are not distributed here. Original project code is MIT licensed; see NOTICE.md for exclusions and attribution.
