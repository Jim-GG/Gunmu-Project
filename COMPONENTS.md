# 通用组件速查

面向关卡搭建的参考文档：这些组件已经做成"拖进场景 → 填几个引用 → 就能用"的形态。
空间坐标、尺寸、数值全部在 Inspector / 场景里配置，玩法脚本不含写死的位置。

---

## 0. 目录与分层

| 目录 | 放什么 | 能不能搬到别的项目 |
|---|---|---|
| `Assets/Scripts/Puzzle/` | 可复用的关卡道具与机关 | ✅ 基本可以（只依赖玩家脚本） |
| `Assets/Scripts/LevelFlow/` | 关卡流程（出口、切关） | ✅ |
| `Assets/Scripts/Player/` | 玩家控制、拳套规则与外观、表现 | ✅ |
| `Assets/Scripts/Enemies/` | 敌人接入玩家框架的适配器（打击 / 遭遇战） | ✅ |
| `Assets/Scripts/`（根） | 全局契约：`GameplayGate`、`IPunchReceiver`、`ILevelExitGate`、`GameStateManager` | ✅ |
| `Assets/Resonator/` | 共鸣者模型、动作、AI（`AI/Runtime`、`AI/Editor`） | ⚠️ 依赖玩家框架的伤害/死亡契约 |
| `Assets/Gauntlet/` | 拳套美术与运行时（模型、动作、控制器、Viewmodel、预览场景） | ✅ |
| `Assets/Prefabs/` | `Player.prefab`、`Pendulum.prefab`、`Seesaw.prefab` | ✅ |

---

## 1. 关卡道具（`Assets/Scripts/Puzzle/`）

### 1.1 `KineticBox` —— 动能方块（核心）

自动附带 `Rigidbody` + `BoxCollider`。拳套击打、摆锤/跷跷板抛射、动量锁定/释放、方块之间传力都靠它。

| 分组 | 关键字段（默认值） |
|---|---|
| 物理 | 接地阻尼 `0.8` / **空中阻尼 `0.08`**（离地时用，越小坠落越重） / 弹射后阻尼 `0.02` / 停止阈值 `0.15` / **自动设置刚体参数**（勾选：冻结旋转+插值+连续检测）/ 弹射低阻尼时长 `1.5` / 锁定可累积最高速度 `40` |
| 滑行物理材质 | 滑行材质（留空=按参数生成）/ 摩擦 `0.05` / 弹性 `0` |
| 判定阈值 | 冲量间隔 `0.1` / 反射间隔 `0.12` / 撞玩家最低速度 `3.5` / 方块传力最低速度 `3.5` / 前瞻余量 `0.15` / 传力横向 `0.9` / 传力纵向 `1.0` / 对锁定方块纵向 `2.0` / 玩家碰撞体尺寸 `(0.95,1.9,0.95)` / 玩家中心高度 `0.9` / 玩家扫掠余量 `0.4` |
| 外观（静态） | 静态材质（留空=场景材质）/ 静态贴图（留空=材质自带）/ 静态状态染色（默认勾选） |
| 外观（特殊状态） | 特殊状态材质（推荐 `Assets/Materials/KineticBox_State.mat`）/ 发光强度 `1.5` / 静态颜色、子弹时间高亮色、瞄准色、锁定基础色、锁定充能色 |
| 动量箭头 | 箭头物体（可拖场景里做好的箭头）/ 箭头预制体（留空=内置方块箭头） |
| 撞击特效 | 撞击特效预制体（留空=不生成）/ 存活时间 `1.6` / **撞击时触发**（`onImpact`） |

**事件 / 对外接口**：`onImpact`、`LastImpactPoint/Normal`；`ApplyCardinalImpulse`、`ApplyFreeImpulse`、`LockMomentum`、`ReleaseMomentum`、`ResetBox`、`SetBulletTimeHighlight`、`IsMomentumLocked`、`StoredSpeed/Direction`。

> 注意：方块默认会把水平速度吸附到 6 个正交轴（"台球感"）。斜向撞弹性墙时，墙收到的是**吸附后**的速度。
>
> 子弹时间高亮色/矄准色默认是 **橙 / 近白**（故意与弹性墙的青绿区分）；方块是运行时生成/销毁的关卡（如 `Soviet_TheCatwalk` 的运输管道）——`PlayerMovement` 会定期重扫方块缓存，不需额外配置。

> 子弹时间 / 动量锁定默认开局可用；`PlayerMovement` 的「开局即拥有动量遥控」（`startsWithMomentumRemote`，默认开）取消勾选后，需要场景里放 `MomentumRemotePickup` 捡到才生效（`Soviet_MomentumShaft` 就是这种教学关）。

---

### 1.2 `BouncyWall` —— 弹性墙

挂在任意带 `Collider` 的物体上即可（Cube、缩放过的方块、斜置墙都行）。**不能是 Trigger。**

| 字段 | 默认 | 说明 |
|---|---|---|
| 反射速度倍率 | `1.0` | 0.5~2.0，>1 会加速 |
| 最低入射速度 | `3.5` | 低于此不弹（方块与玩家共用同一套阈值） |
| 最低垂直入射分量 | `1.0` | 掠射不弹 |
| 表面颜色 / 发光颜色 / 发光强度 | 青色 / 青色 / `2` | 通过 MaterialPropertyBlock 上色 |
| **被弹射时**（`onReflected`） | — | 事件；`LastReflectedPoint/Normal/Velocity/Speed` 可读 |

**反射只用碰撞法线**，所以朝向/尺寸随便改。想让墙面真正发光，用 `Assets/Materials/BouncyWall_Glow.mat`（已开 Emission）；用别的材质时脚本会在运行期复制一份并打开发光。
只有 `KineticBox` 和"按住右键格挡的玩家"会被弹——这是设计上的限定。

---

### 1.3 `PendulumMechanism` —— 巨型摆锤（物理单摆）

`Prefabs/Pendulum.prefab` 结构：`Pendulum`(机制) → `PendulumArm` + `PendulumHammer`。

| 分组 | 关键字段（默认值） |
|---|---|
| 引用 | 支点（留空=自身）/ 锤头碰撞体（留空=找子物体 `PendulumHammer`） |
| 摆动平面 | 摆动轴 `(0,0,1)`（支点父空间）/ 摆长 `0` = 按锤头距离自动 |
| 物理 | 重力倍率 `6`（1 = 真实重力）/ 角阻尼 `0` = 无损耗 |
| 行程 | 起始角 `-65` / 最大摆角 `65` / 自动往复（默认关）/ 复位归位速度 `120` |
| 命中 | 冲击速度 `18.5` / 按实际锤速缩放（默认关）/ 命中后回弹比例 `0.25` / 命中冷却 `0.35` / 锤头判定余量 `1.1` / 命中判定层 |

**用法**：拖预制体到悬挂点 → 摆长自动 → 要么勾"自动往复摆动"，要么用 `PuzzleButton` 的 `targets` 触发。
**调试**：右键组件 → `立即释放摆锤` / `复位摆锤`；选中时 Gizmo 会画出可达弧线与平衡位置。

---

### 1.4 `SeesawMechanism` —— 跷跷板 + 高空起重机

`Prefabs/Seesaw.prefab` 结构：`Seesaw`(机制) → `SeesawFulcrum` + `SeesawPlank` + `CraneWeightBox`。起重机塔/吊臂/缆绳**不在预制体里**，按关卡自己摆。

| 分组 | 关键字段（默认值） |
|---|---|
| 引用 | 跷跷板板身 / 起重臂重物 / 被弹射的方块 / 备用刚体（可空） |
| 跷跷板 | 板身长度 `0` = 用场景缩放（>0 直接拉伸并同步碰撞体）/ 初始倾角 `16` / 翻转后 `-16` / 翻转速度 `180` / 翻转轴 `(0,0,1)` |
| 起重机 | 下落加速度 `28` / 重物初始高度 `0` = 用场景位置（>0 = 相对落点的高度）/ 手动指定落点（勾选后填世界 Y）/ 落点微调 |
| 弹射 | 弹射速度 `22.5` / 弹射方向 `(0,1,0)`（机关局部坐标）/ **弹射时触发**（`onCatapult`） |

落点高度默认**按几何自动推算**（重物底面落在板身对应位置的顶面），整套可以自由移动/旋转。重物砸中后会跟着板身一起下沉。

---

### 1.5 `DashCrystal` —— 充能晶体

| 字段 | 默认 | 说明 |
|---|---|---|
| `respawnCooldown` | `1.5` | 拾取后再生冷却 |
| `rotateSpeed` | `90` | 自转速度（世界 Y） |

玩家触碰 → 拳套能量回满（3 格）。`Collider` 会在 `Awake` 强制设为 Trigger。

---

### 1.6 `PuzzleSwitch` —— 目标开关（关卡终点判定 / 兼职触发源）

| 字段 | 默认 | 说明 |
|---|---|---|
| 颜色（未激活/已激活） | 橙 / 绿 | MaterialPropertyBlock 上色 |
| 移动目标（可空） | — | 激活时按“激活偏移”整体位移（抬门 / 移开指示物） |
| 激活偏移 | `(0,2.5,0)` | — |
| 允许玩家碰触触发 | 勾选 | 关掉则只响应 `KineticBox` |
| **激活时触发的机关**（数组） | 空 | 和按钮一样：任何实现了 `IPuzzleTriggerable` 的组件（摆锤 / 跷跷板 / 以后的新机关）；在 `Activate()` 时触发一次 |
| **开关被激活时** / **开关被复位时** | — | `onActivated` / `onReset` |

Trigger 或实体碰撞都可以触发；被任意 `KineticBox` 击中也会激活。**对 KineticBox 不做筛选**（任何方块都算）。
触发顺序：先触发 `激活时触发的机关`，再抛 `onActivated`。因为激活是一次性的，目标只会被触发一次；`ResetSwitch()` 后可再次激活。

---

### 1.7 `PuzzleButton` —— 通用机关按钮

| 字段 | 默认 | 说明 |
|---|---|---|
| **要触发的机关**（数组） | 空 | 任意实现了 `IPuzzleTriggerable` 的组件（摆锤、跷跷板、以及以后的新机关） |
| **按下时触发** | — | 附加 `onPressed` 事件（开门 / 特效 / 其它） |
| 按 F 可交互距离 | `4.5` | — |
| 允许按 F 触发 / 允许踩踏触发 | 都勾选 | 踩踏对 Trigger 和实体碰撞都生效 |
| 可触发颜色 / 已触发颜色 | 橙 / 绿 | 由目标的 `CanTrigger` 自动切换 |

实现了 `IPuzzleTriggerable` 的机关只要存在，就能被按钮触发，不需要改按钮代码。

---

### 1.8 `KineticBoxDropper` —— 运输管道（按间隔冒方块）

配套预制体：`Prefabs/KineticBoxDropper.prefab`（**只有末端一截管段 + 本脚本**），方块用 `Prefabs/KineticBox.prefab`。

| 分组 | 关键字段（默认值） |
|---|---|
| 引用 | 动能方块预制体（拖 `Prefabs/KineticBox.prefab`） |
| 生成 | 生成间隔 `4.5` 秒 / 首次延迟 `1.2` 秒 / 初速度 `(0,-12,0)`（**相对管口朝向**）/ 出生点偏移 `(0,0.4,0)`（本地）/ 同时最多 `2` 个 |
| 回收 | 低于该世界高度自动销毁 `-25` / 单个最长存活 `30` 秒（0 = 不限） |

- 脚本里没有任何关卡坐标：整个预制体摆到哪里、朝哪里，方块就从那里沿那个方向冒出来（旋转预制体就能改变喷射方向）。
- 掉进深渊的方块会在低于“回收高度”时自动销毁，不会堆积。
- 实现 `IPuzzleResettable`：R 复位时清空已冒出的方块；`GameplayGate.IsBlocked`（暂停 / 死亡）时不生成。

---

### 1.9 `PunchTargetHealth` —— 可打击目标血量

| 字段 | 默认 |
|---|---|
| `maxHealth` | `20` |
| `onDefeated` | — |

拳套命中的伤害由 `PlayerChargedPunch` 按蓄力档位结算。**纯解谜方块不需要挂它**。

---

### 1.10 共鸣者敌人（`Assets/Resonator/`）

预制体：`Resonator/AI/Prefabs/ResonatorAgent.prefab`（根节点：`NavMeshAgent` + `CapsuleCollider` + `ResonatorMotor` + `ResonatorRagdoll` + `ResonatorEnemy` + **`ResonatorPunchTarget`**）。

| 组件 | 位置 | 作用 |
|---|---|---|
| `ResonatorEnemy` | AI/Runtime | 行为状态机（休眠→苏醒→狂暴→行走/奔跑→头槌/拳击/踢击/连招→受击→死亡）、逐帧命中盒、血量与布娃娃 |
| `ResonatorTuning` | AI/Data | 每个敌人变体的数据资产（血量 `120`、视野 `14`、起手距离等） |
| `ResonatorPunchTarget` | Scripts/Enemies | **接入原有框架**：实现 `IPunchReceiver`，冲拳伤害转成 `TakeDamage`（默认 24/40/65/110） |
| `ResonatorTarget` | AI/Runtime | 敌人眼中的“玩家”：`ReceiveStrike` 交给 `PlayerCombatController.ReceiveAttack`，格挡/扣血/死亡全走原有流程 |
| `ResonatorPlayerBridge` | AI/Runtime | 玩家 → AI 的感知：脚步、出拳、蓄力发出噪音（不改玩法） |
| `ResonatorRagdoll` / `ResonatorCrowd` | AI/Runtime | 死亡布娃娃、群体避让与“尸体不阻挡活人” |
| `ResonatorEncounter` | Scripts/Enemies | 逐个放敌人出场，清场后开门；数量/出生点/间隔在 Inspector 配；实现 `ILevelExitGate` 供出口使用 |

**放置要点**：
1. 场景里必须有**已烘焙的 NavMesh**（`Navigation` 物体上挂 `NavMeshSurface`，编辑器里点 Bake），否则敌人不会移动。
2. 玩家身上挂 `ResonatorTarget`（`eyes` 留空 = 自动找相机，`combat` 留空 = 自动找 `PlayerCombatController`）和 `ResonatorPlayerBridge`。
3. 敌人血量与玩家伤害单位不同：玩家 `PlayerCombatController.maxHealth` 建议 `100`，敌人单次命中约 10~25。
4. 调试：Inspector 的**动画工作台**可拖时间轴调 Hitbox 与逐帧事件；`ResonatorArenaHUD` 是独立调试 HUD，正常关卡不需要挂。

---

### 1.11 `Cannon` —— 大炮（吸入 → 蓄压 → 发射）

配套预制体：`Prefabs/Cannon.prefab`（底座 + 可俯仰的炮管 + 侧面填弹仓 + 炮口指示灯）。

> 填弹仓**只用地面上的发光标记框 + 一个 Trigger 体积**标记，没有导轨/挡板实体，贴着墙滑过去的方块不会被卡住（标记框也不带碰撞）。

| 分组 | 关键字段（默认值） |
|---|---|
| 引用 | 炮管 `Pivot`（用它的 forward 当发射方向）/ 发射点 `Muzzle` / 填弹仓触发器 `LoadingBay`（Trigger，留空自动按名字找）/ 炮口指示灯 `ChargeLight` |
| 朝向 | **仰角 `12°`**（正 = 上扬，作用在炮管本地 X 上）；**偏航 = 预制体实例自身的 Y 旋转** |
| 工作参数 | 吸入时长 `0.25s` / 蓄压间隔 `0.55s` / 发射速度 `26 m/s` / **方块发射速度倍率 `0.7`**（方块重力与玩家不同时用来配平射程，重力统一后填 1）/ 装填冷却 `1.2s` / 发射点前方偏置 `0.75m` / 是否吸入玩家（勾选） |

- 检测方式：**每帧用 `Physics.OverlapBox` 轮询填弹仓体积**（不用 Trigger 回调——组件在根节点、触发器在子物体，Unity 的 Trigger 消息不会往上发）。命中的第一个未锁定 `KineticBox` 或 `PlayerMovement` 会被吸进去。
- 方块：吸入期间置为 Kinematic 并拉到炮膛内；发射时用 `ApplyFreeImpulse` 沿炮管方向飞出（自由轨迹、不做 6 轴吸附，仰角会被保留）。
- 玩家：吸入期间暂时禁用 `PlayerMovement` 与 `CharacterController`，发射时瞬移到炮口并调 `PlayerMovement.LaunchAsProjectile(velocity)` 接管速度。
- 发射后进入冷却（避免立刻把自己吸回去）；暂停/死亡会取消装填并放掉手里的东西；实现 `IPuzzleResettable`，R 复位时清空装填。

---

### 1.12 `BouncyWallAngleToggler` —— 可切换角度的弹性墙

挂在弹性墙的**安装座（父物体）**上，用按钮之类的 `IPuzzleTriggerable` 触发，在**两个预设欧拉角**之间来回翻。

| 分组 | 关键字段（默认值） |
|---|---|
| 引用 | 弹性墙转轴 `wallPivot`（真正被旋转的那个 Transform，弹性墙本体是它的子物体） |
| 两个状态 | **状态 A 欧拉角** `(0,135,0)` / **状态 B 欧拉角** `(90,0,0)` / **初始就是状态 A**（不勾 = 开局是 B） |
| 切换 | 旋转过渡时长 `0.25s`（`Slerp`，0 = 瞬切） |

- 设计用途：一面墙既能“斜着把方块弹向侧面”，也能“平放当弹床把玩家弹起来”。
- 两个角度直接填欧拉角即可；面板的**本地 +Z 就是法线方向**（反射结果由 `BouncyWall` 的 `Vector3.Reflect` 决定，和墙摆放的角度天然一致）。
- 实现 `IPuzzleTriggerable`（按钮可触发）与 `IPuzzleResettable`（R 复位回初始状态）。
- 硬件式的注意事项：面板绕转轴转，**斜放时面板两端会扫过一段平面**，转轴要留够空间，别让角插进塔身/墙体。

---

### 1.13 `RisingBridge` —— 升降廊桥

挂在廊桥的**父物体**上，`deck` 指向要升降的桥面，被触发后把桥面从初始位置平移 `raisedOffset` 升到位。

| 分组 | 关键字段（默认值） |
|---|---|
| 目标 | 桥面 `deck`（留空 = 自己）/ 升起位移 `(0,20,0)` / 升起用时 `2.6s` |
| 卡死 | **卡死（永远升不起来）** / 卡死时上抬高度 `0.35m` / 卡死抖动时长 `0.8s` |

- `jammed` 是给关卡剧本用的：开关被触发时这座桥只会“抖一下”，配合另一座正常升起的桥，做出“有一边坏了、得换路”的观感。
- 实现 `IPuzzleTriggerable`（开关/按钮可触发）与 `IPuzzleResettable`（R 复位回收起状态）。
- 桥面是普通带碰撞的块体，升起过程是每帧插值平移；把收起位置放得足够低（≈20m）时，玩家从平台掉下去会触发 `RoomRestartController` 的坠落重开，不会卡在半空的桥上。

---

## 2. 通用契约与流程

### 2.0 `PlayerChargedPunch` + `PlayerGauntletView` —— 拳套（规则 / 外观分离）

| 组件 | 负责 |
|---|---|
| `PlayerChargedPunch` | **玩法规则**：蓄力档位、冲撞距离、冲量、伤害、充能、格挡判定锥 |

- **0 档（无蓄力快拳）不产生任何冲刺位移**，只做一次接触判定：否则玩家能靠零消耗普攻反复推自己上天（无限飞行）。冲撞距离只对 1~3 档生效。
| `PlayerGauntletView` | **外观与动画**：实例化 `GauntletViewModel`，把蓄力/出拳/格挡映射到 `Gauntlet.controller` 的片段 |
| `PunchPresentation` | **打击反馈**：镜头震动、命中顿帧、准星反馈（本项目不使用音效） |

- 正式外观 = `Gauntlet/Prefabs/GauntletViewModel.prefab`（自带 `GauntletAnimationPlayer` + Animator）。
- 帧映射：一档 32 帧、二档 62 帧、满蓄力 92 帧（源动画 30 FPS），在 `PlayerGauntletView` 的 Inspector 可调。
- `GauntletCameraMotion` 会被自动禁用：相机局部姿态只由 `PunchPresentation` 的震屏写入，避免两处抢相机。
- `PlayerGauntletEquipment` 的 `startsEquipped` 决定开局状态（场景里没这个组件时，拳套也是开局可用）：
  - 勾上：开局即可出拳/格挡，外观直接显示；
  - 不勾：需要 `GauntletPickup` 拾取后才显示并可出拳。

`PlayerChargedPunch` 对表现层开放的事件（只读）：`ChargeStarted` / `ChargeChanged(seconds)` / `ChargeCanceled` / `PunchReleased(tier)` / `GuardStarted` / `GuardEnded` / `GuardSucceeded`。
想换新的拳套外观，只需替换 `PlayerGauntletView.viewModelPrefab`，不要动 `PlayerChargedPunch` 的数值。

### 2.1 `GameplayGate`（根目录）

```csharp
public static bool IsBlocked { get; set; }   // true = 暂停/死亡/通关，玩法输入应被忽略
```
所有道具只读它。写入方只有 `GameStateManager`，所以你不需要手动维护。

### 2.2 `GameStateManager`（根目录，常驻单例）

```csharp
enum GameState { Starting, Running, Paused, Dead, LevelCompleted }

GameStateManager.State             // 当前状态
GameStateManager.BlocksGameplay    // == State != Running
GameStateManager.StateChanged      // Action<GameState> 事件
GameStateManager.LastDeathReason

RequestPause() / RequestResume()
NotifyPlayerDied(reason) / NotifyLevelCompleted()
LoadLevel(sceneName) -> bool
RestartCurrentRoom()
```
- 自动创建（`RuntimeInitializeOnLoadMethod`），不需要在场景里放任何东西。
- 唯一负责 `Time.timeScale` 与 `GameplayGate` 的地方。
- `Esc` 在 Running ↔ Paused 之间切换；**新场景载入后自动回到 Running**。

### 2.3 `LevelExit`（`Scripts/LevelFlow/`）—— 关卡出口

| 字段 | 说明 |
|---|---|
| 下一关场景（拖拽，仅编辑器） | 拖 `.unity` 文件，自动同步下面的场景名 |
| 下一关场景名（留空 = 最终关） | 运行时真正使用的值 |
| **前置门禁**（数组，可留空） | 任意实现了 `ILevelExitGate` 的组件（`PuzzleSwitch` / `ResonatorEncounter` / 以后的新条件），**全部满足**才放行 |
| 到达出口时触发 | `onReached` 事件 |

- `Collider` 会被强制设为 Trigger。
- 目标场景必须**在 Build Settings 里被勾选**，否则 `Awake` 报错并禁用自己。
- 留空下一关 → 不切场景，只调用 `NotifyLevelCompleted()`。
- 暂停/死亡（`GameplayGate.IsBlocked`）时不会触发。
- 同时监听 `OnTriggerEnter` 与 `OnTriggerStay`：玩家先站进区域、之后才满足条件也能触发。
- 门禁组件没实现 `ILevelExitGate` 会在 `Awake` 报错并忽略。

> 出口不再区分“解谜出口/战斗出口”：任何解锁条件只要实现 `ILevelExitGate` 就能挂上去。

### 2.4 接口

| 接口 | 实现它有什么好处 |
|---|---|
| `ILevelExitGate` | 能被 `LevelExit` 当作解锁门禁（`bool IsSatisfied`）；目前 `PuzzleSwitch`（解谜）、`ResonatorEncounter`（清场）已实现 |
| `IPuzzleResettable` | 按 R（无 `RoomRestartController` 时）或调 `PuzzleResetUtility.ResetAll()` 会被统一复位 |
| `IPuzzleTriggerable` | 能被 `PuzzleButton` / `PuzzleSwitch` 触发（`bool CanTrigger` + `void Trigger()`） |
| `IPunchReceiver` | 能被冲拳命中（`bool IsFlesh` + `OnPunched(point, normal, direction, tier, strength)`），玩家战斗代码不需要认识具体敌人类型 |

> 目标解析/派发统一走 `PuzzleTriggerUtility`（`Collect` / `TriggerAll` / `AnyCanTrigger`），按钮与开关共用。
> 以后想让新组件也能被触发，只需实现 `IPuzzleTriggerable` 并把它拖进按钮/开关的目标数组。

---

## 3. 常见搭建配方

**① 按钮 → 摆锤**
```
PuzzleButton.targets = [ PendulumPivot ]
PendulumPivot: 起始角 -65 / 最大摆角 65 / 自动往复 关
```

**② 按钮 → 起重机跷跷板 → 弹射方块**
```
PuzzleButton.targets = [ SeesawRoot ]
SeesawRoot.被弹射的方块 = KineticBox_X
（重物位置决定落点，长/高可用 板身长度 / 重物初始高度 微调）
```

**③ 解谜 → 出口 → 下一关**
```
PuzzleSwitch.onActivated ─（可选：接特效 / 播放过场）
LevelExit.前置门禁     = [ 上面那个 PuzzleSwitch ]
LevelExit.下一关场景   = 下一关的 .unity
```
战斗关同理：`LevelExit.前置门禁 = [ ResonatorEncounter ]`，清场后自动放行。

**④ 被拳打飞的方块 → 撞开关 → 顺便触发机关**
```
KineticBox + PuzzleSwitch（两者都带 Collider；开关建议 Trigger）
PuzzleSwitch.激活时触发的机关 = [ PendulumPivot ]   // 开关也能当触发源
```

**⑤ 格挡弹射飞越深渊**
```
BouncyWall（斜置、非 Trigger、倍率 1.1~1.2）
玩家空中按住右键格挡撞墙 → 反射并回满蓄力
```

---

## 4. 事件一览

| 组件 | 事件 | 时机 |
|---|---|---|
| `KineticBox` | `onImpact` | 撞上弹性墙（读 `LastImpactPoint/Normal`） |
| `BouncyWall` | `onReflected` | 被方块/玩家弹射 |
| `PuzzleSwitch` | `onActivated` / `onReset` | 开关激活 / 复位 |
| `PuzzleButton` | `onPressed` | 按 F 或踩踏 |
| `SeesawMechanism` | `onCatapult` | 板身翻转、方块被弹出 |
| `PunchTargetHealth` | `onDefeated` | 血量归零 |
| `GameStateManager` | `StateChanged` | 状态切换 |
| `PlayerChargedPunch` | `ChargeStarted` / `ChargeChanged` / `PunchReleased` / `GuardStarted` / `GuardEnded` / `GuardSucceeded` | 蓄力、出拳、格挡（表现层订阅） |
| `ResonatorEnemy` | `StateChanged` / `StrikeResolved` / `Died` / `FrameEventReceived` | 敌人状态、命中结算、死亡 |
| `ResonatorPunchTarget` | — | 实现 `IPunchReceiver`，无需额外事件 |

---

## 5. 已知限制 / 容易踩的坑

1. **发光需要材质开启 Emission 关键字**。现成的有 `Materials/KineticBox_State.mat`、`Materials/BouncyWall_Glow.mat`；`KineticBox` 未指定特殊状态材质时会打警告，`BouncyWall` 会在运行期复制材质并自动打开发光。
2. **弹性墙不能是 Trigger**，否则完全不生效。
3. **摆锤/跷跷板的尺寸**：优先改 `摆长`、`板身长度`、`重物初始高度` 这类参数，而不是直接缩放预制体（缩放会让摆长/落点跟着变，虽然会自动测量但不好维护）。
4. **Build Settings 必须勾选目标场景**，否则 `LevelExit` 的 `Awake` 会报错并禁用自己。当前已勾选：`Test_01_PunchThrough` / `Test_02_Combat` / `Test_03_Billiards` / `Soviet_1` / `Soviet_TheCatwalk` / `Soviet_GetTheBox` / `Soviet_Traffic`。
5. **两套 R 重置并存**：挂了 `RoomRestartController` → R 重载场景；没挂 → R 原位复位（`PuzzleResetUtility.ResetAll()`）。别在一个关卡里同时依赖两种语义。
6. **KineticBox 的 6 轴吸附**：默认会把水平速度吸附到 ±X/±Z，斜向撞墙的反射方向以吸附后的速度为准。
7. **箱体、门、抬升机关的自身位移**都是用 `PuzzleSwitch` 的“移动目标 + 激活偏移”实现的；没有专门的位移组件，需要更复杂的过场就用 Timeline / Animator。
8. **拳套外观与规则分开**：改手感去 `PlayerChargedPunch`，换美术去 `PlayerGauntletView`（只需换 `viewModelPrefab`）。
9. **拳套美术的预览场景**：`Assets/Gauntlet/Scenes/GauntletPreview.unity` 不依赖游戏玩法脚本，按 1-4 / C / I / P / K / H / O 或左侧按钮直接播片段；`GauntletCameraMotion` 只在预览里有意义，正式玩家由 `PlayerGauntletView` 自动禁用（避免和震屏抢相机）。
10. **子弹时间高亮要能撑住“动态生成的方块”**：`PlayerMovement` 的 `allBoxes` 缓存会定期重扫（并且进入子弹时间、出现空槽时立即重扫）。早期版本只在 `Start()` 扫一次，导致 `Soviet_TheCatwalk` 这种全部由 `KineticBoxDropper` 运行时冒方块、旧方块又被回收的关卡完全不高亮。以后再加“运行时生成/销毁方块”的关卡不用改代码。

---

## 6. 代码入口（按需要看）

| 想改什么 | 去哪 |
|---|---|
| 方块手感 / 判定窗口 | `Puzzle/KineticBox.cs` 顶部字段 |
| 墙面反弹规则 | `Puzzle/BouncyWall.cs` 的 `TryReflect` |
| 摆锤物理/命中 | `Puzzle/PendulumMechanism.cs` 的 `Simulate` |
| 跷跷板降落/翻转/弹射 | `Puzzle/SeesawMechanism.cs` |
| 按钮触发链 | `Puzzle/PuzzleButton.cs` + `IPuzzleTriggerable` |
| 状态机 / 暂停 / 切关 | `GameStateManager.cs` |
| 拳套伤害与冲撞距离 | `Player/PlayerChargedPunch.cs` |
| 拳套外观 / 动画映射 | `Player/PlayerGauntletView.cs` |
| 敌人行为与命中盒 | `Resonator/AI/Runtime/ResonatorEnemy.cs` + `ResonatorTuning.cs` |
| 敌人受伤 / 刷怪 | `Scripts/Enemies/ResonatorPunchTarget.cs` + `ResonatorEncounter.cs` |
| 关卡出口 / 解锁条件 | `LevelFlow/LevelExit.cs` + `ILevelExitGate` |

---

## 7. 当前关卡

| 场景 | 内容 | 用到的组件 |
|---|---|---|
| `Scenes/Soviet_Traffic.unity` | **正式关卡（白模）**：“中”字形纵深关。出生平台（大炮 + 方块）—（升降廊桥A）—中央塔（同高度环绕钢走廊）—（升降廊桥B·卡死）—终点平台；东侧隔着空档是开关平台。中央塔南面挂一面**可在“斜放/平放”之间切换的弹性墙**，按钮就在墙边。流程：把方块塞进大炮打出 → 子弹时间锁定在半空当挡板 → 自己坐大炮飞过去撞在方块上落到钢走廊 → 按按钮把墙切成斜放 → 放掉方块，方块被弹到开关平台撞开关（两座廊桥一起触发，B 卡死）→ 再按按钮把墙复位成平放 → 沿升起的廊桥A走回出生平台 → 再坐大炮飞过去落在平放的墙上被弹起 → 飞过塔顶落到终点平台，穿过升起的终点门到达出口 | `BouncyWall` + `BouncyWallAngleToggler` + `PuzzleButton` + `Cannon` + `KineticBox` + `PuzzleSwitch`（同时抬起终点门）+ `RisingBridge`×2（其中一座 jammed）+ `LevelExit`（门后） |
| `Scenes/Soviet_GetTheBox.unity` | **正式关卡（白模）**：从起点房间出来到窄平台（带坡塌廊桥残段）→ 坐起点大炮沿 +Z 射出 → 撞拐角那面**小、斜向上**的弹性墙 → 弹到终点的窄平台（同样是断桥残段）→ 把方块推进终点大炮、自己也坐进去 → 沿 −X 射出、再撞同一面弹性墙 → 回到起点平台 → 把方块轰进起点房间里触发开关（终点门打开）→ 再坐起点大炮飞回终点进门 | `Cannon`×2 + `BouncyWall`（小斜面板）+ `KineticBox` + `PuzzleSwitch` + `DashCrystal` + `LevelExit`（门禁 = Switch） |
| `Scenes/Soviet_1.unity` | **正式关卡（白模）**：坠落的电梯里出生 → 捡拳套 → 从破墙洞出去 → 深渊上方的 L 形钢走廊 → 轰开堵住门洞的动能方块 → 在房间里把方块从门禁室钢条下方打进去撞开关 → 抬门出口 | `GauntletPickup` + `PlayerGauntletEquipment` + `KineticBox` + `DashCrystal` + `PuzzleSwitch` + `LevelExit`（门禁 = Switch） |
| `Scenes/Soviet_TheCatwalk.unity` | **正式关卡（白模）**：拳套能量 0 出生在房间里 → 进入 36×36 的高墙深渊大厅 → 沿“中”字竖笔的钢廊桥走 → 断口对面挂着塔塌桥体 → 右侧高处运输管道不时冒出带初速度的动能方块，落到斜向弹性墙上被弹向断口中央横飞而过 → `Q` 子弹时间 + `E` 锁定它做垫脚跳过去 → 走完对面廊桥，从墙上的门进入房间 | `KineticBoxDropper`（+`KineticBox` 预制体）+ `BouncyWall` + `LevelExit` |
| `Scenes/Test_01_PunchThrough.unity` | 测试关卡：捡起拳套 → 一拳轰开堵住 1.6 米通道的方块 → 走到出口 | `GauntletPickup` + `PlayerGauntletEquipment` + `KineticBox` + `LevelExit` |
| `Scenes/Test_02_Combat.unity` | 测试关卡：清掉三只共鸣者，门打开后出口生效 | `ResonatorEncounter` + `ResonatorAgent` + `NavMeshSurface` + `LevelExit`（门禁 = Encounter） |
| `Scenes/Test_03_Billiards.unity` | 测试关卡：拿晶体充能 → 把方块横向顶到定位挡条对齐 → 从背后一拳送进窄缝 → 击开关抬门 | `KineticBox` + `DashCrystal` + `PuzzleSwitch` + `LevelExit`（门禁 = Switch） |

- 链路：`Test_01_PunchThrough` → `Test_02_Combat` → `Test_03_Billiards`（测试关链）；正式关卡 `Soviet_1` → `Soviet_TheCatwalk` 已接通，`Soviet_GetTheBox` 与 `Soviet_Traffic` 目前 `nextSceneName` 留空（= 通知通关，后续再接）。
- 六个场景（含 `Soviet_Traffic`）都是直接在编辑器里堆基本体块 + 拖组件搭的，没有生成器；想改地图直接在场景里拖物体/改引用即可，不必改脚本。
- `Test_02_Combat` 需要 `Navigation` 物体（`NavMeshSurface`）重新 Bake 才能让敌人正确寻路；`Soviet_1` / `Soviet_TheCatwalk` 目前没有敌人，不需要 NavMesh。
