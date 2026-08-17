# npcPlayer 0.2.2 使用手册

本文面向使用 Lönn 制作 Celeste 地图的作者，介绍如何在地图中生成由 TAS 输入驱动的 `Player` 型 NPC。

npcPlayer 当前属于技术预览版。建议先在独立测试房间验证所需实体和其他 Helper 的兼容性，再用于正式流程。

## 1. 它能做什么

npcPlayer 会在房间中生成真正继承自 `Celeste.Player` 的 NPC，并用地图模组内的直接输入 TAS 文件驱动它。NPC 因此可以使用大部分原版 Player 移动能力，例如行走、跳跃、冲刺、攀爬和抓取。

它不是剧情 NPC、寻路系统或 CelesteTAS 播放器。运行时不依赖 CelesteTAS；CelesteTAS 只适合在制作阶段录制、观察和调整输入。

当前主要特性：

- 一个房间可以放置多个不同 `npcId` 的 NPC。
- 同一个 NPC 可以有多个出生点，并在死亡重载后切换出生点。
- TAS 使用压缩段保存和执行，不会为每一帧创建一个对象。
- 解压安装地图模组时，可以热更新 TAS 和皮肤配置。
- 内置 Madeline、Badeline 外观，并可选用 SkinModHelperPlus 的 `SkinName`。
- NPC 可以与常见地形、移动平台、可抓取物和部分原版 `PlayerCollider` 实体交互。

## 2. 安装与依赖

将 npcPlayer 的发行 ZIP 直接放入：

```text
Celeste/Mods/
```

不要解压 npcPlayer 自身的发行包。重启 Celeste 和 Lönn 后，在 Lönn 中应能找到：

- 实体：`npcPlayer (Spawn Point)`
- Trigger：`Activate npcPlayer`
- Trigger：`Change npcPlayer Respawn`

地图模组的 `everest.yaml` 至少应包含：

```yaml
- Name: YourMap
  Version: 1.0.0
  Dependencies:
    - Name: Everest
      Version: 1.0.0
    - Name: npcPlayer
      Version: 0.2.2
```

只有使用 SkinModHelperPlus 自定义皮肤时，地图模组才需要额外声明对应依赖。使用内置 `madeline` 或 `badeline` 时不需要安装 SkinModHelperPlus。

## 3. 最小可运行示例

以下示例创建一个名为 `partner` 的 NPC。当真人进入 Trigger 后，NPC向右移动 30 帧、跳跃，然后冲刺。

### 3.1 放置出生点

在房间中放置 `npcPlayer (Spawn Point)`：

```text
npcId: partner
default: true
```

### 3.2 创建 TAS 文件

在地图模组中创建：

```text
Tas/YourName/YourMap/partner_intro.tas
```

内容：

```tas
30,R
1,R,J
12,R
1,R,X
20,R
```

### 3.3 放置激活 Trigger

放置 `Activate npcPlayer`：

```text
npcId: partner
tas: Tas/YourName/YourMap/partner_intro.tas
once: true
playerOnly: true
```

进入游戏后，房间加载会生成 `partner`；真人第一次进入 Trigger 时开始播放 TAS。

## 4. 推荐的地图模组结构

```text
YourMod/
  Maps/
    Author/
      Map.bin
  Tas/
    Author/
      Map/
        partner_intro.tas
        partner_escape.tas
  config/
    npcPlayer/
      npcPlayer.yaml
  everest.yaml
  Dialog/
  Graphics/
```

TAS 和配置属于使用 npcPlayer 的地图模组，不应放入 npcPlayer 本体的 ZIP。

推荐使用 `Tas/<作者>/<地图>/<动作>.tas`，避免大型模组中的文件重名。

## 5. NPC 出生点

Lönn 实体 ID：

```text
npcPlayer/npcPlayerSpawnPoint
```

字段：

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `npcId` | 字符串 | 空 | NPC 的逻辑身份，区分大小写 |
| `default` | 布尔值 | `false` | 是否优先作为该 NPC 在本房间的初始出生点 |

房间加载时，每个不同的 `npcId` 只生成一个运行时 NPC。同一个 `npcId` 可以对应多个出生点：

- 存在 `default=true` 时，使用地图数据顺序中的第一个默认出生点。
- 没有默认出生点时，使用地图数据顺序中的第一个出生点。
- 同一个 ID 有多个默认出生点不会报错，但只使用第一个。
- 不同大小写视为不同 ID，例如 `Partner` 和 `partner` 是两个 NPC。
- 空白 `npcId` 会被规范为 `npc`。

在 Lönn 中，npcPlayer 的每个 `npcId` 字段都会从当前打开地图的所有 npcPlayer Entity 与 Trigger 收集 ID，并提供可搜索候选。候选只用于辅助编辑：字段仍允许输入新的 ID 或有意不匹配现有出生点的值。

运行时 NPC 只属于当前房间。跨房间移动和跟随尚未实现；切换房间时，NPC 会根据新房间的出生点重新创建。

## 6. NPC 配置

外观与死亡连接使用地图模组中的可选配置文件：

```text
config/npcPlayer/npcPlayer.yaml
```

示例：

```yaml
npcPlayer:
  - npcId: "partner"
    variant: "madeline"
    death_link:
      lead_by_player: true
      lead_to_player: false
  - npcId: "rival"
    variant: "badeline"
    death_link:
      lead_by_player: false
      lead_to_player: false
  - npcId: "guide"
    variant: "MySmhPlusSkinName"
```

两条死亡连接是以真人 Player 为中心的独立有向边：

- `lead_by_player` 决定真人死亡是否导致该 NPC 死亡。
- `lead_to_player` 决定该 NPC 死亡是否导致真人死亡。
- NPC 之间没有直接死亡连接。NPC A 杀死真人后，只会通过真人死亡继续影响自身 `lead_by_player` 为 `true` 的其他 NPC。

规则：

- `npcId` 必须与出生点完全一致，包括大小写。
- `madeline` 和 `badeline` 是 npcPlayer 提供的内置外观名称，大小写不敏感。
- 其他 `variant` 会作为 SkinModHelperPlus `SkinModHelperConfig.yaml` 中的原始 `SkinName` 查询。
- 没有配置的 NPC 默认使用 Badeline，两条死亡连接均默认为 `true`。
- `death_link` 或其中某个布尔值缺失、非法时，受影响的字段独立回退为 `true`，不会丢弃合法的 `variant`。
- 找不到自定义 `SkinName` 时会记录警告并回退到 Badeline。
- 同一 `npcId` 重复配置时，第一条有效配置生效，其余条目被忽略并记录警告。
- 格式错误的条目会被跳过；整个文件无法解析时，本模组中的 NPC 都使用默认外观和死亡连接。

继续兼容 v0.2.0 格式；旧格式条目的两条死亡连接均视为 `true`：

```yaml
- npcPlayer:
    npcId: "partner"
    variant: "madeline"
```

SkinModHelperPlus 是可选依赖。npcPlayer 通过经过验证的兼容层读取其皮肤信息；若未来版本改变内部元数据结构，npcPlayer 会记录带版本号的警告，并保留完整的 SMH+ 管理状态，避免产生半应用皮肤。

## 7. TAS 文件格式

npcPlayer 接受直接帧输入行：

> **录制建议：** 使用 CelesteTAS 录制脚本时，建议通过游戏的“拓展异变（Extended Variants）”关闭冻结帧效果。npcPlayer 自身不会产生与真人完全相同的冻结帧；如果按保留冻结帧的真人流程录制，回放时的输入时序可能发生偏移，导致 NPC 的实际行为与录制预期不一致。

```text
帧数,输入,输入,...
```

例如：

```tas
# 向右移动
30,R

# 向右跳
1,R,J
20,R

# 向右冲刺
1,R,X
15,R

# 抓住右侧墙面
10,R,G
```

支持的输入：

| 输入 | 同义写法 | 含义 |
| --- | --- | --- |
| `L` | — | 左 |
| `R` | — | 右 |
| `U` | — | 上 |
| `D` | — | 下 |
| `J` | `K` | 跳跃 |
| `X` | `C` | 冲刺 |
| `G` | `H` | 抓取 |
| `Z` | `V` | 蹲伏冲刺 |

方向输入会组合成标准化方向。例如 `U,R` 会得到右上方向，不是两个独立的完整强度轴。

解析规则：

- 帧数必须是大于零的整数。
- 输入名称不区分大小写。
- 空行会被忽略。
- 以 `#` 或 `//` 开头的普通注释会被忽略。
- 未知输入会让整个 TAS 加载失败，并在 `log.txt` 中指出文件和行号。
- TAS 结束后，NPC 输入回到空状态并停在当前位置。
- 激活新的 TAS 会立即中断旧 TAS，从新文件的第一帧开始。

当前不支持：

- CelesteTAS 命令。
- `Read`、`Repeat`、`RecordCount`。
- Savestate 指令。
- 模拟摇杆精确数值。
- TAS 段落标签。
- 旧版 `#npcPlayer...` 分段语法。该语法会明确报错，不会偷偷拼接多个段落。

安全限制：

- 单个文件最多 100,000 个输入段。
- 单个文件最多累计 10,000,000 帧。
- TAS 路径必须位于当前地图模组内部。
- 禁止绝对路径、盘符、空路径以及 `.`、`..` 路径段。
- 省略 `.tas` 扩展名时会自动补充，但推荐显式写出。

## 8. Activate npcPlayer

Lönn Trigger ID：

```text
npcPlayer/activateNpcPlayer
```

字段：

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `npcId` | 字符串 | 空 | 要控制的 NPC，区分大小写 |
| `tas` | 字符串 | 空 | 相对于地图模组根目录的 TAS 路径 |
| `once` | 布尔值 | `true` | 成功启动一次后是否停用 |
| `playerOnly` | 布尔值 | `true` | 是否只允许真人激活 |

对于名为 `MapName.bin` 的地图，Lönn 会在当前地图模组中扫描直接位于 `Tas/**/MapName/` 目录下的 TAS 文件。匹配的 `.tas` 文件会显示在可搜索列表中，同时仍允许输入任意路径。Windows 界面使用 `\` 方便阅读；选中的路径和手动输入的路径在写入地图数据前都会规范化为跨平台的 `/`。

`once` 只在 TAS 成功找到并解析后才会消耗。NPC 不存在、地图来源无法识别、路径不安全或 TAS 解析失败时，Trigger 可以再次尝试，并会在日志中写明原因。

当 `playerOnly=false` 时，真人和 npcPlayer 都可以激活它。普通 Celeste Trigger 默认不会响应 npcPlayer；这是 npcPlayer 自带的明确兼容例外。

注意避免制作 NPC 自己反复进入且 `once=false` 的循环触发区域，否则同一个 TAS 可能不断从第一帧重新开始。

## 9. Change npcPlayer Respawn

Lönn Trigger ID：

```text
npcPlayer/changeNpcPlayerRespawn
```

字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `npcId` | 字符串 | 要修改出生点的 NPC，区分大小写 |
| Node | 坐标 | 必须恰好有一个 Node |

真人进入 Trigger 后，npcPlayer 会：

1. 查找本房间中 `npcId` 完全匹配的所有出生点。
2. 选择距离 Node 最近的出生点。
3. 将该选择保存为本房间内的重生位置。

它不会立即传送活着的 NPC。新位置会在下一次本房间重载时生效，例如死亡重试。离开本房间后，另一个房间会重新使用自身的默认出生点规则。

当前该 Trigger 没有实现 `INpcPlayerTrigger`，因此只有真人会触发它。

## 10. 热更新工作流

开发时应将地图模组解压安装，例如：

```text
Celeste/Mods/YourMod/
```

Everest 不会实时监视 ZIP 内的文件，所以压缩地图包不适合热更新调试。

### TAS 热更新

每次成功进入 `Activate npcPlayer` 时，都会重新读取并解析当前 TAS：

- 新增或修改 TAS 后，不需要重启 Celeste。
- 已经播放中的 TAS 使用启动时快照，不会在中途改变。
- 要运行修改后的内容，应再次激活该 Trigger。若 `once=true` 且本房间已经成功使用过它，需要重载房间；频繁调试时可暂时设置 `once=false`。
- 删除或写坏 TAS 不会破坏正在播放的旧快照，但下一次激活会失败并写入日志。

### NPC 配置热更新

修改 `config/npcPlayer/npcPlayer.yaml` 后，npcPlayer 会使该地图模组的配置缓存失效：

- 不需要完全重启 Celeste。
- 已存在的 NPC 不会原地更换皮肤或死亡连接。
- 下一次房间加载、死亡重试，或离开后重新进入时应用新配置。
- 不原地重建是为了保留当前坐标、持有物和 TAS 状态。

## 11. 交互与兼容边界

### 地形和可抓取物

npcPlayer 使用原版 Player 物理，可与 `Solid`、`JumpThru` 和常见移动平台交互。部分只通过 `Tracker<Player>` 查找真人的原版逻辑由兼容层补充。

NPC 和真人争抢同一个 Holdable 时，先成功持有者拥有它；另一方不能把它直接抢走。

### Trigger

普通原版及第三方 `Trigger` 默认忽略 NPC。原因是原版 `Trigger.Triggered` 和 `PlayerIsInside` 是单真人状态，让多个 Player 共享它们会造成错误的 `OnEnter`、`OnStay` 或 `OnLeave`。

npcPlayer 为兼容 Trigger 使用独立状态机。第三方模组若确认自己的 Trigger 对 NPC 安全，可以实现：

```csharp
using Celeste;
using Celeste.Mod.Entities;
using Celeste.Mod.NpcPlayer.Runtime;
using Microsoft.Xna.Framework;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

public sealed class MyTrigger : Trigger, INpcPlayerTrigger
{
    public MyTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
    }

    public bool AllowsNpcPlayer(NpcPlayerEntity player) => true;
}
```

回调执行时，npcPlayer 会临时提供符合原版语义的 Trigger 状态，并在回调结束后恢复。实现方不应缓存或在回调外依赖这些共享字段。

### PlayerCollider

以下类型的原版交互被明确允许，包括常见危险物、弹簧、Booster、Bumper、羽毛和水晶补充物。收藏品、门、剧情与进度实体默认忽略 NPC，避免改写 Session 或剧情状态。

第三方实体不会因为继承某个原版类型而自动获得许可。确认安全后应显式实现：

```csharp
using Celeste.Mod.NpcPlayer.Runtime;
using Monocle;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

public sealed class MyEntity : Entity, INpcPlayerCollider
{
    public bool AllowsNpcPlayer(NpcPlayerEntity player) => true;
}
```

接口只决定实体的 `PlayerCollider` 回调是否允许执行；具体碰撞行为仍由实体自身实现。

## 12. 全局状态隔离

NPC 更新期间：

- 使用独立、未绑定真实手柄的虚拟输入。
- 不允许移动相机。
- 不允许改变全局水下音乐状态。
- 不产生手柄震动。
- 同步发起的全局 Freeze 会被丢弃。
- 可归属到该 NPC 的空间音效依据其与真人的直线距离调整：64 像素内保持完整音量，随后平滑衰减，在 320 像素处静音。

附着在 NPC 上的循环 `SoundSource` 会随 NPC 移动持续更新增益。无位置信息的音乐、环境音、Snapshot、UI 音频以及真人自身音效均不受影响；无法找到真人时保持原始音量。

单个 NPC 会保留自身正常的内部混音，即使它的多个合法事件有短暂重叠也不会启用群体限流。只有多个不同 npcPlayer 所有者同时可听时才分配预算：它们的距离增益共享 1.2 个满音量 NPC 的等效预算。播放同一事件路径的 NPC 所有者还会共享一个满音量事件预算，且只保留距真人最近的四个所有者可听。同一 NPC 在同一帧重复调用相同事件的 `Player.Play` 时，后续实例仍会作为重复声部静音。降低增益立即生效；竞争 NPC 结束后约用 80 ms 平滑恢复，避免音量突跳。

游戏自身或真人发起的全局 Freeze 仍会暂停场景，NPC 的 TAS 游标也会一起暂停。死亡体稍后产生的死亡反馈不属于普通 NPC 更新隔离范围。

## 13. 死亡与重载

死亡传导是以真人 Player 为中心的有向星型关系：

- 真人死亡时，仅使 `lead_by_player` 为 `true` 的存活 NPC 进入死亡流程。
- NPC 死亡时，仅在该 NPC 的 `lead_to_player` 为 `true` 时导致真人死亡。
- NPC 杀死真人后，真人死亡只能继续传导到各自 `lead_by_player` 为 `true` 的其他 NPC；NPC 之间没有直接死亡连接。
- `lead_to_player: false` 的 NPC 会独立死亡，并且当前房间生命周期内不会自动重生。
- 只有真人死亡会记录统计并负责最终屏幕擦除和房间重载；NPC 死亡体完成视觉效果后不会独立重载房间。

真人死亡并重载房间后，即使某个 NPC 的 `lead_by_player` 已关闭，它仍会根据出生点数据重新创建。关闭该连接只会阻止本次 NPC 死亡事件和死亡体，不会让运行时 NPC 跨房间重载保留。

技术预览版仍应重点测试自定义皮肤、多个 NPC 同时死亡及第三方死亡钩子的组合。若视觉效果与预期不符，请同时提供皮肤名和 `log.txt`。

## 14. 常见问题

### 房间中没有出现 NPC

检查：

1. 地图模组是否依赖 `npcPlayer 0.2.2`。
2. 是否放置了 `npcPlayer (Spawn Point)`。
3. Lönn 中的实体 ID 是否仍为 `npcPlayer/npcPlayerSpawnPoint`。
4. `log.txt` 是否出现重复 ID、皮肤配置或地图来源错误。

### Trigger 找不到 NPC

`npcId` 区分大小写。确认出生点、外观配置和 Trigger 三处完全一致，且目标 NPC 的出生点位于当前房间。

### TAS 没有播放

检查：

- `tas` 路径是否相对于地图模组根目录。
- 文件是否确实属于包含当前地图的同一个模组。
- 是否使用正斜杠和 `.tas` 扩展名。
- 是否包含不支持的 CelesteTAS 命令、段落标签或零/负帧数。
- `log.txt` 中是否报告了具体文件与行号。

### 修改 TAS 后行为没变

正在播放的 TAS 不会中途替换。使用 `once=false` 的 Trigger 再次进入，或用另一个 Trigger 重新启动它；若原 Trigger 已以 `once=true` 成功使用，则先重载房间。地图模组必须是解压目录，ZIP 文件不会热更新。

### 修改 NPC 配置后行为没变

外观和死亡连接只在创建 NPC 时应用。死亡重试、重新进入房间或以其他方式重新加载房间后再观察结果。

### NPC 不触发某个第三方实体

如果它是 Trigger，需要第三方实现 `INpcPlayerTrigger`；如果依赖 `PlayerCollider`，需要实现 `INpcPlayerCollider`。这通常是安全边界，不代表 NPC 没有发生空间碰撞。

### NPC 改变了剧情或收集进度吗

默认不会。收藏品、门、剧情和进度类 `PlayerCollider` 不在许可列表中，普通 Trigger 也不会响应 NPC。第三方显式选择兼容后，其回调副作用由第三方实现负责。

## 15. 报告问题

技术预览反馈请至少附带：

- Celeste 版本。
- Everest 版本。
- npcPlayer 版本。
- SkinModHelperPlus 版本和使用的 `SkinName`，如果适用。
- 相关 Helper 名称及版本。
- 完整 `log.txt`。
- 最小复现房间或地图。
- 对应 TAS 与 `config/npcPlayer/npcPlayer.yaml`。
- 预期行为、实际行为和稳定复现步骤。

如果问题只在特定第三方实体上出现，请说明它使用的是 Trigger、PlayerCollider、Solid、Holdable，还是其他自定义交互方式。

## 16. 可复制示例

仓库中的 `examples/YourMod/` 提供了最小目录结构、`everest.yaml`、外观配置和 TAS 样例。复制后请修改模组名称、地图路径、NPC ID 和皮肤名称，不要直接以 `YourMap` 发布。
