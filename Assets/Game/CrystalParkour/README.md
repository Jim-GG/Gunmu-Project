# 晶体冲击跑酷 Demo

**当前版本为 V2。** 打开 `Assets/Game/Scene/CrystalTraversalLab_V2.unity`。两个间歇减重力筒仓、三种水平风道、独立无风晶体区、远程敲墙生晶、错峰下降无人机均已实现。

完整试玩路线、拖模块搭建、Inspector 参数、几何节点改资产：见仓库 `Docs/CrystalParkour/V2/关卡搭建与参数指南.md`，也可在 Unity 中用 `Tools → Crystal Parkour → Open V2 level design guide` 打开。

模块在 `Prefabs/LevelModules`；新晶体在 `Assets/Game/Environment/crystal/Prefabs/V2`；Blender 作者文件在 `ArtSource/Crystal/V2/Crystal_Environmental_GeometryNodes.blend`。旧关卡保留；下面是 V1 路线记录，其中旧风力字段已由 V2 的“剩余重力 / 水平漂移 / 间歇开关”替代，无人机新版采用下降队列。

打开 `Assets/Game/Scene/CrystalImpactParkour.unity`，按 Play，点击 Game 画面锁定鼠标。无需重新搭场景或生成资源。

## 操作与路线

- WASD 移动，Shift 奔跑，Space 跳跃。
- 按住左键对准可交互晶体，会沿可通行路径牵引。进入约 1.1 米击打距离才开始 2 秒计时，牵引途中不消耗这个时间。
- 在 2 秒内松开左键。先播放原拳套出拳，真正的 Hitbox 命中晶体时才反弹；在 1.95 秒释放也会等挥击到达有效帧，不会因动画前摇突然超时。
- 超时会脱附，需要松开后重新蓄力；同一块晶体不能在空中连续刷反弹。视角不会被自动转向。
- R 返回最近检查点，清空移动冲量、拳套动作和预输入；Esc 释放鼠标。

试玩顺序：

1. 走近入口地面的蓝色裂缝板，低头用拳套击打，晶体平台从地面长出，门打开。
2. 利用平台跃上入口台阶，进入上升风场。对准两侧墙面晶体，按住左键牵引，攀附后松开反弹，配合 WASD 向前调整。
3. 到达风洞出口检查点，走近右侧墙面裂缝板并击打。长出的水平晶体平台可以把附近玩家托起，再跃向无人机登乘平台。
4. 跳到双涵道无人机的金属踏板上；它会载着玩家往返移动。无人机是可替换的占位模型，中央发光容器不是踩踏目标。
5. 借无人机跃上尽头平台。反弹、风洞、地面生晶、墙面生晶、无人机借力均完成后，终点显示完成提示。

## Inspector 调整入口

| 组件 / 资源 | 可调内容 |
| --- | --- |
| 玩家 `CrystalTraversal` | 牵引范围和速度、附着距离、2 秒窗口、命中等待时间、重复附着冷却、跌落复位高度 |
| `CrystalSurface` | 是否可牵引、法线方向弹射速度、向上速度、切向动量保留比例 |
| `WindVolume` | 方向、加速度、上升速度上限、进入和离开的渐变时间、启用状态；BoxCollider 定义范围 |
| `CrystalGrowthSite` | 生长对象、方向、时长、破出距离；生长结束前关闭平台碰撞，避免卡进玩家 |
| `TraversalDrone` | 起终点、速度、停留时间、悬停幅度、甲板碰撞体、左右转子引用 |
| `Data/ParkourGauntletActions.asset` | 在原动画工作台调整命中有效帧、动画速度、衔接和事件 |

Demo 使用独立的拳套 Action Set 副本，沿用已有动画、蓄力档位和格挡逻辑；攻击范围略向前延伸以可靠敲地面，并关闭战斗选敌吸附，避免与晶体牵引争夺位移。未修改原拳套配置、敌人或旧关卡。

原 `FirstPersonMotor` 仍负责角色碰撞与移动，新增可选外部加速度、保留冲量和暂停普通运动的接口。未挂跑酷组件时，这些输入默认为零 / 关闭。

## 资源

- 晶体模型、14 个预制体、3 个共享材质：`Assets/Game/Environment/crystal`。
- `CrystalAnchor_Wall` 和 `CrystalPlatform_Standable` 为可交互预制体，其余为装饰变种。生长方向默认为局部 Y，墙面使用时将 Y 朝向房间内部。
- Blender 作者工程：`ArtSource/Crystal/Crystal_ModularKit.blend`；源模型及载入成品前的副本在 `ArtSource/Crystal/Source`。
- 讨论依据与验证：`Docs/CrystalParkour`。

`Tools → Crystal Parkour → Build New Demo` 是首次构建入口；已有同名场景时会拒绝覆盖，以保护手动调整。日常直接打开场景、修改 Inspector 即可。
