# 全域酿造箱 / Global Keg Chest 1.0.2

[English instructions below](#english)

下载：[GitHub Releases](https://github.com/xx20031129-a11y/GlobalKegChest/releases) · [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/54040)。请下载安装 ZIP，源码压缩包不能直接作为 Mod 安装。

把原料放进全域酿造箱，它会为所有地点的原版小桶自动装料，将完成的饮品收回箱子。箱子与小桶之间没有距离限制，覆盖建筑内部、姜岛，以及游戏当前维护的其他地点。

## 获取与使用

1. 通过 SMAPI 启动游戏。本版的构建和验证基线为游戏 1.6.15、SMAPI 4.5.2。
   游戏语言设为简体中文时，箱子名称、描述和制作配方会显示中文；在标题画面切换语言后也会同步更新。
2. 基础耕种达到 8 级后，在制作菜单中寻找“全域酿造箱”。已经达到 8 级的角色会在读档或次日补学配方。
3. 制作材料：木材 ×150、铜锭 ×3、铁锭 ×1、橡树树脂 ×1、电池组 ×1、铱锭 ×1。
4. 放置箱子并存入小桶能接受的原料。默认在每日开始、每 10 个游戏分钟检查一次。
5. 成品会存入有空间的酿造箱。箱子满时，成品留在小桶中；腾出空间后，下一次检查会继续处理。

每只箱子 70 格，原料与成品共用空间；多个箱子分别保存自己的库存，共同服务全部小桶。只使用已放置的酿造箱，普通箱子与背包中的箱子道具不参与。

如果希望先加工某种原料，将它放在箱子中靠前的位置。跨箱子的顺序为地点内部名称、地图格子 Y、X；不按距离或价格选料。相同且可堆叠的原料可以跨箱合并，品质不同的原料不会混合。例如两只箱子内的 3+2 颗普通咖啡豆可供一只小桶加工。

箱子使用固定的木质、小桶徽记外观。它只服务原版小桶 `(BC)12`，不负责陈酿桶、罐头瓶或其他 Mod 新增独立 ID 的机器。加工时间和产出遵从游戏当前的小桶规则。睡觉期间不额外模拟多轮加工，次日按游戏结算后的机器状态继续。

## 安装

解压安装 ZIP，把其中的 `GlobalKegChest` 文件夹放进游戏的 `Mods` 目录。必需文件为两个 DLL、manifest、图像和翻译；无需 Content Patcher 或 SpaceCore。更新或安装时关闭游戏。

联机自动化仅由房主执行，各玩家应安装相同版本。箱子被玩家打开时会跳过该箱子。与 Chests Anywhere 共用原生箱子库存，但实际两人联机、分屏和远程开箱界面的兼容性仍需游戏内确认；本版不将这些场景描述为已经实测通过。

## 配置

首次启动后，Mod 文件夹生成 `config.json`。修改后重新启动游戏。

```json
{
  "Enabled": true,
  "ProcessIntervalMinutes": 10,
  "VerboseLogging": false
}
```

`Enabled` 控制自动化；`ProcessIntervalMinutes` 可设为 10–120 之间的 10 的倍数，错误值回退为 10；`VerboseLogging` 输出每轮处理数量和耗时。联机时由房主的配置决定调度。关闭自动化不会删除物品，箱子仍可以使用。

## SMAPI 控制台命令

- `gkc status`：显示酿造箱、小桶、空闲/加工中/可收获数量。
- `gkc rescan`：房主重新扫描并执行一次处理，也会解除出现异常后的小桶暂停。
- `gkc give`：给本地玩家一个酿造箱，便于测试；背包满时拒绝领取。

出现异常时，相关小桶暂停至次日或手动重新扫描，其他小桶继续工作。请保留 SMAPI 日志。

## 卸载

先关闭自动化，取出专用箱子中的全部物品，收起并处理掉所有全域酿造箱道具，然后保存退出，删除 `Mods/GlobalKegChest`。原版小桶中已经开始的加工仍使用游戏规则继续。不要在保留自定义箱子物品的情况下直接移除 Mod。

## 开发验证

源码目录包含 53 项独立规则测试及使用真实 1.6.15 程序集、XNB 数据的自动检查，覆盖装料、跨箱合并、容量、收获、异常回滚、箱子 XML 往返、跨地点索引和远程锁。调度器的房主/分屏门控检查使用受控的 SMAPI 状态；它们不等同于真实网络联机测试。运行程序集检查不启动游戏窗口、不读取或写入玩家存档。

构建与验证命令在源码根目录运行，`GamePath` 改成实际游戏目录：

```powershell
dotnet test tests/GlobalKegChest.Tests -c Release
dotnet run --project tests/GlobalKegChest.RuntimeChecks -c Release '-p:GamePath=C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley' -p:EnableModZip=false
./tools/package.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley'
```

纯规则测试无需游戏；程序集检查和 Mod 构建需要本机游戏及 SMAPI。详细证据见源码中的 `docs/acceptance.md`。

## 许可与兼容旧存档

本项目的原创代码、文档和箱子贴图采用 [MIT 许可证](LICENSE)。安装包不包含游戏文件或其他 Mod 的素材。公开版的 SMAPI Mod ID 为 `xx.GlobalKegChest`；旧版已创建的箱子仍保留内部物品 ID `Local.GlobalKegChest_Chest`，以便现有存档继续识别。请不要自行修改该物品 ID。

开发使用了 AI 辅助；功能与兼容性以本文列出的验证范围为准。

## English

Download the install ZIP from [GitHub Releases](https://github.com/xx20031129-a11y/GlobalKegChest/releases) or [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/54040). GitHub's automatically generated source archive is not an installable mod.

Global Keg Chest automatically supplies and collects every placed **vanilla keg** across locations. A chest has 70 slots shared by ingredients and finished products; multiple chests keep separate inventories but serve the same keg network. Products remain safely in their kegs if the chests are full.

The recipe unlocks at base Farming level 8. It costs **150 Wood, 3 Copper Bars, 1 Iron Bar, 1 Oak Resin, 1 Battery Pack, and 1 Iridium Bar**. Automation runs at day start and every 10 in-game minutes by default, using the game's native keg recipes and processing times. Inputs are considered in chest/location order and slot order. Compatible coffee beans can be combined across chests; different qualities are kept separate.

**Install:** Requires Stardew Valley 1.6.15 or newer and SMAPI 4.5.2 or newer. Unzip the release archive into `Stardew Valley/Mods`, making sure the path `Mods/GlobalKegChest/manifest.json` exists, then start the game through SMAPI. Content Patcher and SpaceCore are not required. Updating should be done while the game is closed.

The mod handles vanilla kegs `(BC)12`, including kegs in building interiors and other current game locations. It does not handle casks, preserves jars, or custom machines with other IDs. The host alone runs automation in multiplayer, and all players should install the same version. This release has automated rule and real-assembly checks plus Windows/SMAPI startup checks; actual crafting-menu use, two-player play, split-screen, Chests Anywhere remote opening, and use together with Automate have not yet been manually verified. Please report issues with reproduction steps and a SMAPI log.

Configuration is generated as `config.json` after first launch: `Enabled` defaults to `true`; `ProcessIntervalMinutes` defaults to `10` and accepts multiples of 10 from 10 to 120; `VerboseLogging` defaults to `false`. SMAPI console command `gkc` accepts `status`, `rescan`, and `give`. English and Simplified Chinese text follow the game's language. Before uninstalling, remove items from all Global Keg Chests, pick up or dispose of the chests, save, and then remove the mod.

The project code, documentation, and original chest artwork are licensed under [MIT](LICENSE). The public SMAPI ID is `xx.GlobalKegChest`; the chest's internal item ID remains `Local.GlobalKegChest_Chest` to protect existing saves made with the local 1.0.1 build.

AI tools assisted development. The validation scope and untested scenarios are stated above.
