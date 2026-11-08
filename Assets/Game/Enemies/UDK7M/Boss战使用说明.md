# UDK7M Boss 战：试玩和修改

打开 `Assets/Game/Scene/UDK7MBossArena.unity`，点击 Play。进入实验区后开战；R 重置整场战斗。入口提示与 HUD 会显示剩余晶柱、共鸣者数量和充能时间。

## 战斗循环

从后方绕过装置，诱导它撞击四根晶柱之一。每根只能用一次，提供 15 秒持续补足格挡能量的模式，并启动三名共鸣者的补位池。三级蓄力现在为 160 伤害，击杀共鸣者后立即布娃娃并击飞。让尸体进入旋转钻头约 5 米范围，它会被吸入绞碎，每具造成 140 点 Boss 伤害，约八具击败装置。

大共振对爆心 6 米内的共鸣者造成 130 伤害，让其他存活者进入 Agonizing，不伤玩家。普通死亡 3–5 秒补位，大共振击杀 15–20 秒补位。只有死亡尸体会被钻头吸引；补怪不会提前回收现有尸体。

ParryStart 内命中为完美格挡、零伤害；其他格挡阶段承受 34% 伤害。两种都增加 1 点格挡能量，重招仍可能击退。

## 在工作台把 Hitbox 绑到任意骨骼

菜单 `Tools > UDK7M > Open Boss Animation Workbench` 可直接打开 SlamRecover；也可选中 `Actions/UDK7MActions.asset`，Inspector 点“打开共用动作工作台”。选中命中盒，点击“从模型搜索 / 选择骨骼…”，搜索“钻芯”或 `DrillRoll`。选择后会自动设为 Custom Bone 并记录完整路径。事件的声音和特效也使用同一选择器。

`DrillRoll` 是实际旋转钻芯；`Tool` 是整个钻具；`Control_Tip` 是导出的 IK 目标参考骨。实际接触盒优先选 DrillRoll。拖黄色中心点移动盒体，彩色控制点调整尺寸，按住 Shift 拖动彩色点绕对应轴旋转；局部旋转字段也可直接输入，尺寸单位始终为米。

| 字段 | 用途 |
|---|---|
| Repeat / Once | 一次动作内，同命中组对同目标只结算一次 |
| Repeat / Effects Only | 只检测接触、产生特效，不扣血 |
| Repeat / Cooldown | 可持续扣血，但同目标受 Damage Cooldown 限制 |
| Repeat / Every Sample | 每次采样可重新扣血，多个碰撞体仍合并为同一目标 |
| Sample Every Frames | 每隔几个动画源帧采样；默认旋转接触为 2 |
| Only While Rotating | 仅骨骼相对父级沿 Rotation Axis 旋转时启用 |
| Contact Effects | 接触实体场景也产生反馈 |
| Contact Effect / Sound | 可替换接触特效预制体和声音，运行时使用有上限的对象池 |
| Effect Cooldown | 限制同一碰撞体的接触反馈频率 |
| Knockback Distance | 固定位移距离；-1 时按伤害 × Knockback Per Damage 计算 |
| Blocked Knockback Multiplier | 格挡后保留多少击退；0 表示格挡后不击退 |

SlamRecover 默认重击 3–8 帧、40 伤害、3 米击退；旋转接触独立为每 2 帧检测、3 伤害、0.25 秒冷却。TripleSlam 有三个不同命中组，第三下第 59 帧的 `ThirdSlam` 信号触发落石。想改落石触发时机，直接移动这个事件。

原有手调配置备份在 `Actions/UDK7M_BeforeBoss.asset`；原始转移快照还在 `AssetStaging/UDK7M/BeforeBossImplementation`。不要把备份当成当前运行配置。

## 调整 Boss 和关卡

- `AI/UDK7M_Default.asset`：血量、招式权重、转速、8 m/s 冲锋、距离触发、刹车曲线、吸入范围、伤害及补怪冷却。
- `Actions/UDK7MActions.asset`：动画速度、帧范围、混合、命中盒、事件。SlamRecover 的移动由 AI 刹车曲线控制；Clip 本身没有水平根位移。
- `AI/UDK7M_Resonator.asset`：仅供本场战斗使用，400–520 帧起身约 4 秒；其他关卡的共鸣者起身配置不变。
- 场景的 `UDK7M Boss Encounter`：绑定 Boss、玩家、晶柱、补怪组件和检查点。选中后可直接打开工作台或重新烘焙导航。
- `Facility Geometry`：大型 Boss 导航；子级 `Main arena modules`：共鸣者导航。下层和格栅使用 ArenaLowerFloor 层，两种导航都排除它。

自己改布局时可移动晶柱、删改地面模块、调整出生锚点。出生点要在可行走主地面；系统会等待它处于玩家视野外且无遮挡空间足够时才生成。改变柱子或地面后，在 Encounter 的 Inspector 点击“重新烘焙本场景导航”，再保存场景。

履带默认按照整机实际位移和左右转向独立滚动，受阻时不会继续按冲锋速度空转；机械臂动作时间不受它影响。`Tracks Follow Movement` 可切回原始动画中的履带表现，`Track Distance Multiplier` 可校准滚动距离。

液体持续伤害，但不是即死。下层晶体带上抛反弹参数，可牵引脱困。落石预警锁定后不追踪玩家，其中两块落地后可拾取充能；血液特效里的小碎晶只是视觉效果，不提供能量。

Boss 击败后保留根物体并触发 On Defeated，留给后续剧情接续。本场景不包含后续追逐情节。
