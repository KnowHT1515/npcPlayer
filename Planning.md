# Planning

    [completed]1. 新增 `npcPlayer-Player` 存活/死亡状态解绑选项，更新 `npcPlayer.yaml` 配置方式，新格式规范为:

    ```yaml
    npcPlayer:
      - npcId: "<npc_id>"
        variant: "<SkinName>"
        death_link: 
          lead_by_player: <bool>
          lead_to_player: <bool>
      - npcId: "<npc_id>"
        variant: "<SkinName>"
    ```

    依照 `npcId` 逐个配置各 `npcPlayer` 的行为

    `variant` 行为与规范不变。新增 `death_link` 字段，`lead_by_player` 意为由玩家死亡引起的 NPC 死亡事件，`lead_to_player` 意为由 NPC 死亡引起的玩家死亡事件，二者独立配置，默认值均为 `true`

    当前暂不设计 `lead_to_player:false` 下 NPC 死亡后的自动重生机制

    `death_link` 字段缺省、非法值等情况均回退默认值

    同時兼容 `v0.2.0` 旧用法，使用旧用法時，默认 `death_link` 字段均为 `true`

    死亡传导以真人 `Player` 为中心：`lead_to_player` 表示 `NPC -> Player`，`lead_by_player` 表示 `Player -> NPC`。NPC 死亡导致真人死亡后，只会继续影响各自 `lead_by_player:true` 的其他 NPC；NPC 之间不建立直接死亡连接。

    `lead_to_player:false` 的 NPC 独立死亡后不会触发擦屏、房间重载或真人死亡统计，完成死亡视觉效果后从当前场景移除，直到房间因其他原因重新加载时才会按出生点重新创建。

2. 处理 `npcPlayer` 产生的各类音效，防止音效叠加导致的爆音等问题，目前考虑依据 `npcPlayer` 距离玩家的直线距离影响音效增益

3. 优化 `Lonn` 中的操作体验：

对于 `npcId`: 拟对 `npcPlayer` 中所有涉及 `npcId` 的 `Trigger` 与 `Entity` 添加快速填充功能，同时保留自定义编辑的能力。即，对于 `Mod/Maps/MapGroup/MapName.bin` ，所有在 `MapName.bin` 中出现过的 `npcId` 都将允许通过额外的展开栏快速选中对应值，或输入部分值后进行筛选再选择，或快速补全，而不需要每次都重复手动输入完整值

对于 `Tas`: 拟对 `npcPlayer` 中所有涉及 `Tas` 文件的 `Trigger` 与 `Entity` 添加自动扫描功能，同时保留自定义编辑的能力。即，对于 `Mod/Maps/MapGroup/MapName.bin` ，所有的 `Mod/Tas/**/MapName/*.tas` 文件都将被扫描并制成可展开的快速选择栏，或输入部分值后进行筛选再选择，或快速补全，而不需要每次都重复手动输入完整值。同时注意处理正斜杠与反斜杠，内部应当归一化为统一的 `/` 正斜杠表示法，而在 `Windows` 上继续显示 `\` 反斜杠

同时注意处理若展开条目过多导致的展开栏边界问题