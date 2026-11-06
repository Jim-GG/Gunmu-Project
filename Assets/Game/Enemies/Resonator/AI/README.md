# 共鸣者 AI 与动画调节

测试场景：`Assets/Game/Enemies/Resonator/AI/Scenes/ResonatorCombatArena.unity`。包含五种初始行为、已烘焙 NavMesh、拳套玩家、障碍物和状态 HUD。N 发出测试声音，R 恢复玩家生命值。敌人预制体：`AI/Prefabs/ResonatorAgent.prefab`。

## 群体寻路、尸体与攻击起手

活着的共鸣者采用 NavMesh 的高质量局部避让，并给予不同的避让优先级。导航避让半径为 Motor 身体半径加 `Crowd Padding`，与实际身体之间留有余量。物理移动检测不再把其他共鸣者当成静态墙；身体间距由软分离和移动约束维护，避免相互穿过。持续缺乏进展时，会尝试一个受导航和世界碰撞限制的临时侧移目标，再继续追击。

死亡后移除导航代理；尸体骨骼继续与地面、场景做布娃娃物理，但不再阻挡活着的共鸣者的移动，也不遮挡其感知/攻击视线。新生成的敌人同样会忽略已存在的尸体。尸体不会被转换成把窄路封死的 NavMesh 障碍洞。

每个攻击的 **AI 最大起手距离** 在工作台动作页和 Tuning 的 Attack Beat 中可调：头槌默认 1.6m、拳击 1.7m、跑近踢击 1.75m。这里是双方根节点的水平间距上限，原有 Hitbox 接触范围保持不变，接近后的位移仍由动作轨道执行。Attack 保持踢击后连招派生，不会从普通状态直接起手；其距离字段不改变这个派生限制。0 使用旧的全局 `Attack Enter Distance`。

AI 先选定下一招，按该招范围追近，朝向和视线合格后才起手。会估计玩家后退速度以及从目标切入帧到首个命中窗口的前摇时间，适当收紧移动目标的起手距离，减少过早空挥。玩家持续后退时，会比旧的纯距离门槛更早改为 Running；Running 仍然只接 Kicking。

Tuning 的 **Crowd Navigation** 可调 `Crowd Padding`、身体间隙、分离速度、避让优先级及差异、卡住多久尝试脱困、侧移距离和持续时间。**Attack Planning** 可调起手朝向角度、后退预测强度、最长预测时间与提前奔跑的速度阈值。运行时敌人 Inspector 会显示计划动作、当前起手距离和侧移脱困次数。升级前配置备份在 `AI/Validation/Resonator_BeforeCrowdNavigation.asset`。

固定场景障碍应包含在 NavMesh 烘焙中。需要动态改变路线的箱子等可用 [NavMeshObstacle Carving](https://docs.unity3d.com/cn/current/ScriptReference/AI.NavMeshObstacle-carving.html) 更新导航可通行区域。群体侧移用于局部拥堵恢复，不替代关卡导航数据。

`CrowdNavigationScenarios` 在 Unity 实际导航循环中验证：三名初始重叠的追兵都向目标推进、通过布娃娃尸体、遇局部小阻挡侧移恢复、实体墙仍阻挡攻击、约 1.7 米开始踢击、头槌起手距离、朝向/视线限制，以及对后退目标的时机判断。

## 动画工作台：可视混合、Hitbox 和事件

### 攻击过程中的追击位移

动作页新增 **攻击位移与追踪**。Headbutt、Punching、Kicking、Attack 已分别配置缓慢逼近+头槌前压、踏步、前滑、爆发前冲的预设。只改变水平移动和转向，保留你已调整的动作速度、命中盒与衔接规则。升级前备份在 `AI/Validation/Resonator_BeforeAttackDisplacement.asset`。

勾选 **预览完整位移（无碰撞）**，拖动时间轴查看模型实际前进；调整 **玩家初始距离** 比较近距离不前推、距离较远时补足位移。底部青色轨道是追踪窗口，紫色轨道是每段位移，红色仍为命中窗口；橙色地面轨迹显示直线补距预览。

| 调节项 | 用途 |
| --- | --- |
| 开始 / 结束帧 | 在动画的哪一段踏步或前冲，可以添加多段 |
| 最大补足距离 | 此段最多额外走多少米，实际按目标距离缩短 |
| 最大速度 | 实际游戏时间的米/秒，限制突进速度和积压补距 |
| 期望接触间距 | 敌人与目标根节点的水平间距；足够近时停止前推，也不会低于全局 Minimum Separation |
| 累计位移进度 | 横轴是这段动画的 0–1 进度，纵轴是已经释放的位移比例；曲线斜率越陡移动越快，平段暂停 |
| 追踪开始帧 / 锁定方向帧 | 前摇持续更新目标与转向；锁定后向记录的位置出手，不再无限跟随闪避中的玩家 |
| 转向速度 / 前方角度 | 限制追踪转身速度和可跟随范围 |
| 叠加原动画根位移 | 默认关闭，避免源动画的后撤抵消新加的追击；关闭整条轨道则恢复原有根位移逻辑 |

位移由统一 Motor 提交，查询 NavMesh 路径，再受导航边界、实体墙壁、速度上限和玩家间距约束，最终同步 `NavMeshAgent.nextPosition`。AI 在攻击期间仍然处于攻击状态，不切回 Walking/Running。受到攻击、死亡或进入下一招时会中止当前追击段。实际接触到激活的命中盒时也会停止前推。补距总量只统计真正移动的距离，遇墙不会瞬移；跳过的动画切入帧不会补放先前位移。重叠位移段按列表顺序选择第一条。

默认最大补距：Headbutt 两段合计 0.95m、Punching 0.9m、Kicking 0.65m、Attack 1.8m；这些是上限，近距离攻击可能只前进几厘米或完全不前进。若希望更能追击后退中的玩家，可以适当增加补距、速度或延后锁定帧；想保留更多横向闪避空间则提前锁定。参考体和轨迹是无碰撞、静止目标的预览，游戏中仍可能被障碍、目标闪避或命中盒位置影响。

`AttackDisplacementScenarios` 包含移动目标对照：同一攻击和命中盒对持续后退目标，原地模式打空，追击模式命中；另验证间距、补距上限、速度、跟随与锁定、墙壁、受击中断、NavMesh 同步与预览预算。

这里实现的是本项目的曲线追击方案。通用的目标适配思路可参考 [Epic Motion Warping 官方文档](https://dev.epicgames.com/documentation/unreal-engine/motion-warping-in-unreal-engine)，不代表对《艾尔登法环》内部算法的确认。

菜单 **Tools > Resonator > Animation Workbench 动画与命中盒**，或敌人/Tuning Inspector 的 **打开动画工作台** 按钮。窗口使用隔离的预览模型，不改变场景中敌人的姿态。可在编辑模式使用，不必开始游戏。

**动作 · Hitbox · 逐帧事件** 页：选动作，播放、慢放、逐帧前后移动，或拖动底部源动画帧时间轴。红条表示各命中窗口，黄线表示事件。预览里的红色体积表示当前激活的 Hitbox，灰色表示关闭；蓝色胶囊只是距敌人 1.05m 的玩家参考体，不代表每次攻击一定命中。

命中盒可选 Box / Sphere / Capsule，绑定根节点、头、胸、左右手脚或自定义骨骼名称。调整开始/结束帧（包含边界）、位置、局部旋转、完整尺寸或半径/高度、伤害和目标 Layer。**黄色中心点拖动位置，红绿蓝尺寸点拖动大小**，也可直接输入数值；偏移沿绑定骨骼的坐标，尺寸以米计，不会乘 FBX 内部的 130 倍骨骼缩放。自定义骨骼必须输入准确名称，缺失的骨骼不会造成伤害。加多个窗口可在不同帧使用不同位置和大小；每个窗口对当前 AI 目标最多命中一次。多个体积属于同一次挥击时，填入相同的非空 **命中组**，合计只扣一次血；留空则各窗口独立结算。

实际伤害必须同时满足激活帧、实际物理体积与目标 Collider 重叠、目标层和无遮挡。已移除距离/角度直接扣血逻辑。Minimum Blend Weight 可要求新动作达到指定权重才允许伤害。窗口被切入帧跳过、或动作被提前取消后，不会补算历史伤害。低帧率时按源动画帧细分采样，并精确经过窗口边界，避免用收招姿态补算早先的攻击；这不是对高速移动目标的连续碰撞检测。

动作页可开启 **预览根位移（无碰撞）**，观察原动作位移对攻击范围的影响；默认原地预览。预览不执行导航、避障和玩家间距修正，游戏中的根位移仍由 Motor 限制。

攻击收尾已直接连接工作台规则：**当前 Attack 命中后 → Attack → Screaming；打空后 → Combat Idle**。`Any → Combat Idle` 是公共后备，Punching 等攻击也会使用；存在精确 From→Combat Idle 时，精确规则优先。前一招 Kicking 命中，不会把后续打空的 Attack 判成命中。Kicking→Attack 和仍在继续的 Headbutt 连段保留优先级；Any 恢复规则不会打断受击、死亡或普通移动。

收尾规则新增 **收尾衔接帧（-1 自动）**：`-1` 在当前攻击真正命中后立即开始 Screaming 过渡；打空则等最后一个有效命中窗口关闭后进入 Combat Idle。填写源动画帧号可要求至少等到该帧，保留想要的后摇。此参数作用于实际运行和预览，不需要勾选“自动连招取消”。混合秒数和目标起始帧使用你在该规则里保存的值。自动模式的预览用首个命中窗口开始估算成功时机，实际游戏仍需物理碰撞。

**两段动画混合预览** 页：选择 From→To 规则，拖动过渡时间，查看过渡前、混合中和过渡后；蓝条来源、绿条目标。可以调整 Blend、Entry Frame、攻击连招取消帧或收尾衔接帧，预览使用同一个运行时混合器。普通状态的“来源切出帧”仅控制预览；自动连招预览取对应分支的取消帧，攻击收尾使用上述真实收尾帧。Any Source 规则可选一个实际来源状态。窗口不替代完整 Animator 状态图。

**逐帧事件**：每个动作的事件列表点击 +，标记会放在当前帧。Signal 触发敌人 Inspector 的 **On Animation Event(string)**，可拖组件接收名称；C# 可订阅 `enemy.FrameEventReceived += marker => { ... }`，读取 name、payload、value。Sound 指定 AudioClip，value 是音量；Effect 指定 Prefab、绑定部位和偏移、自动销毁时间。事件按帧顺序触发，每次经过只触发一次，循环会重新触发，跳过的入场帧之前事件不会补发。**编辑预览不执行事件、不生成特效、不播放音效、不造成伤害。** 播放中的事件结构修改在下次播放该动作时重新排序。

编辑支持 Undo/Redo；点击窗口右上 **保存资产** 保存到当前 Tuning，建议复制资产作为不同敌人变体。升级前的资产备份在 `AI/Validation/Resonator_BeforeHitboxEditor*.asset`。自动迁移只校正仍符合旧默认配置的窗口，已调整的窗口保持原值。

## 调整动画混合、连招和动作范围

选中场景中的敌人，在 Inspector 点击 **编辑动画衔接 / 连招取消参数**，进入它引用的 `Resonator_Default` Tuning 资产。建议复制资产，再分配给不同敌人变体。此 AI 通过 Playables 播放，调节的是这个资产，不是旧预览 Animator Controller 的箭头。

每个 Animation Beat 可以独立设置 Start Frame、End Frame（-1 表示片段末尾）、Loop、Speed、Blend（秒）。`Transitions` 数组覆盖某一条 From → To 衔接，可自行增加规则；精确规则优先于 Any Source，相同来源和目标只使用第一条启用的规则。

| 字段 | 用途 |
| --- | --- |
| 混合时长（秒） | 仅调整这条衔接；0 为直接切换，无规则时使用目标 Beat 的 Blend |
| 目标起始帧 | 从目标动画哪一帧进入；-1 使用其 Beat 的 Start Frame |
| 命中后取消帧 | 当前攻击命中后，最早从哪一帧取消进入下一个攻击；-1 等到动作结束 |
| 未命中取消帧 | 未命中时的独立取消时机，-1 等到动作结束 |
| 自动连招取消 | 到达取消帧便自动衔接；关闭后可由代码调用 `TryCancelAttack(nextState)` 请求取消 |

升级后的默认快速衔接为：Headbutt 命中第 43 帧／未命中第 53 帧 → Kicking 第 12 帧，混合 0.06 秒；Kicking 命中第 29 帧／未命中第 33 帧 → Attack 第 20 帧，混合 0.055 秒。目标入场调整保留实际踢击的起脚前摇。Headbutt 是否继续仍受概率和连击距离限制。Kicking 即使禁用提前取消，也会在结束后接 Attack。Running 只接 Kicking；普通近战不直接启动 Attack；Punching 默认独立。具体以当前 Tuning 中用户修改后的数值为准。

可以增加其他攻击之间的取消规则来试验不同连段，数组顺序决定同时可用时的选择。不要设置同状态的自动取消或互相无条件立刻取消，否则会反复重启攻击。取消帧用**来源动作**的帧号，目标起始帧用**目标动作**的帧号，通常 30 FPS。跳过命中窗口会跳过对应伤害，提前取消也会舍弃未执行的后续窗口，因此调整时一起查看 `Hits` 的 Start/End Frame。

未完成的混合被再次打断时，会从当前完整混合姿态继续过渡。运行时敌人 Inspector 显示当前帧、是否命中，并提供测试取消、正面受击、背面受击和死亡按钮。播放时可即时修改 Tuning；需要保留的重要数值建议记下并在退出播放后核对保存。

## 状态与感知

| 初始行为 | 待机与唤醒 |
| --- | --- |
| FeignDeath | 冻结真实倒地开头；只听声音。Wake 到 520 帧切 Scream |
| Feeding | 循环 150–420；听见后从 421 起身，到 490 切 Scream |
| SlowIdle | 循环 0–700；视觉、听觉均可触发 Scream |
| RestlessIdle | 循环 0–425；视觉、听觉均可触发 Scream |
| Resonating | 面向 Crystal Focus 循环 Headbutt；听见后 Agonizing，以 20°/秒朝玩家转向 |

Scream 后根据距离追逐或攻击。跑步只接踢击，踢击后接 Attack。收尾攻击命中回 Scream；打空进入 0.6–1.5 秒 CombatIdle，再走近或跑近。受击直接打断攻击，正面 Reaction Hit、侧后方 Stumbling；受击结束直接跑近或 Headbutt。优先级为死亡 > 受击 > 攻击 > 移动。

`Hits` 提供独立的帧窗口、空间体积和伤害。玩家格挡会调用现有拳套 TryBlock，阻止伤害并增加格挡能量。拳套攻击通过 Player Bridge 命中敌人；步行、奔跑、攻击和蓄力会发出不同半径的声音。

## 移动、物理与接入其他场景

Walking/Running 从原动画提取根位移轨迹、计算速度，再由 NavMesh 寻路。动画播放速度跟实际速度同步。攻击、受击及死亡前的动作使用提取轨迹，统一由 Motor 限制墙壁、导航边界和与玩家的最小间距。Animator 不再重复驱动根位置。

存活骨架不添加 Rigidbody/Joint，避免缩放骨架上的物理插值干扰蒙皮。死亡第 50 帧停止动画，再创建物理刚体与关节接管姿态。旧八个 FBX 的开头绑定姿态帧已排除，装死开头能保持真实倒地。

接入其他关卡：放入 Agent 预制体并烘焙 NavMesh，选择 Initial Activity；共鸣状态分配 Crystal Focus；给玩家添加 ResonatorTarget，并引用 Eyes、Vitals、Gauntlet；添加 ResonatorPlayerBridge，引用 Motor、Combat 和 Attack Origin。自定义声音通过 `ResonatorNoise.Emit(position, radius, target)` 发出，伤害调用 `enemy.TakeDamage(damage, sourcePosition, impulse)`。默认视觉可遮挡、听觉按半径判定，声音不做墙体衰减。

重新导入动作后，使用菜单 `Tools > Resonator > AI > 1 Extract Motion And Build Enemy` 更新轨迹和预制体；会保留已有 Tuning 数值。`2 Build Test Arena` 会生成测试场景，不应用来覆盖正在手工修改的关卡。

验证脚本在项目根目录 `Docs/Resonator/Validation`，通过 Unity Pipeline 的 `script run` 在测试场景播放模式运行。ScenarioReport、骨架稳定性报告和实际渲染截图在 `AI/Validation`。这些验证覆盖行为与骨架稳定性；每条过渡的美术节奏仍可按上述参数逐条调整。

新增 `AnimationAuthoringScenarios` 验证真实体积查询、前摇无伤害、逐帧激活、跳帧、命中组、事件顺序和预览混合；`WorkbenchPreviewScenarios` 比对编辑器窗口和运行时的根节点、骨骼位置与混合姿态。状态图集成测试使用可重复的接触体积保证命中/未命中分支，不代表所有美术配置都必然命中参考玩家。
