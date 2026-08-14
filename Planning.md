# Planning

1. 新增 `npcPlayer-Player` 存活/死亡状态解绑选项，更新 `npcPlayer.yaml` 配置方式，新格式规范为:

```yaml
npcPlayer:
  - npcId: "<npc_id>"
    varaint: "<SkinName>"
    death_link: 
      lead_by_player: <bool>
      lead_to_player: <bool>
  - npcId: "<npc_id>"
    varaint: "<SkinName>"
```

依照 `npcId` 逐个配置各 `npcPlayer` 的行为

`varaint` 行为与规范不变。新增 `death_link` 字段，`lead_by_player` 意为由玩家死亡引起的 NPC 死亡事件，`lead_to_player` 意为由 NPC 死亡引起的玩家死亡事件，二者独立配置，默认值均为 `true`

当前暂不设计 `lead_to_player:false` 下 NPC 死亡后的自动重生机制

`death_link` 字段缺省、非法值等情况均回退默认值

同時兼容 `v0.2.0` 旧用法，使用旧用法時，默认 `death_link` 字段均为 `true`