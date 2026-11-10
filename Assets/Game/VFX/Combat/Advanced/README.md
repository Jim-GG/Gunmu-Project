# 战斗特效：调整入口

统一配置在 `Resources/AdvancedCombatVFX.asset`。Boss 和拳套初始化时读取这个资源；编辑 Prefab、Graph 或这里的引用即可，不需要重新运行资源生成脚本。

## 找到效果

| 效果 | 资源 |
| --- | --- |
| 普通接触火星及中心亮点 | `../Sparks.prefab` → `Graphs/VFX_SparksFine.vfx` |
| 小爆炸 | `Prefabs/ExplosionSmallGPU.prefab` → `Graphs/VFX_ExplosionSmall.vfx` |
| 大爆炸 | `Prefabs/ExplosionFinalGPU.prefab` → `Graphs/VFX_ExplosionFinal.vfx` |
| 晶体碎片、碎片尾烟 | `Prefabs/CrystalShatterGPU.prefab` → `Graphs/VFX_CrystalShatterGPU.vfx` |
| 履带移动烟尘 | `Graphs/VFX_TrackDust.vfx`，由 Boss 上 `UDK7MPresentation` 创建左右两份 |
| 冲锋风锥 | `Prefabs/ChargeWindCone.prefab`，内部嵌套 `../ReconstructedWindCone/Prefabs/风锥.prefab` |
| 爆炸扬尘 | `Prefabs/ExpandingGroundDust_Explosion.prefab`，嵌套原 Heavy，仅启用 `Expanding ground dust` |
| 色差折射球 | `Prefabs/ChromaticSphere.prefab`、`Graphs/SG_ChromaticDistortion.shadergraph` |
| 新热浪 | `Prefabs/HeatHaze_Loop.prefab`、`HeatHaze_Burst.prefab`、`Graphs/SG_HeatHaze_URP.shadergraph` |
| 气罐蒸汽和热浪 | `Prefabs/TankSteamAdvanced.prefab` |
| 拳套流动能量 | `Materials/M_WeaponSurfaceEnergy.mat`、`Graphs/SG_WeaponSurfaceEnergy.shadergraph` |
| 满蓄力红闪 | `Prefabs/GauntletFullFlash.prefab` → `Graphs/VFX_GauntletFullFlash.vfx` |
| 高台落地冲击 | 原 `../ConcreteImpactHeavy.prefab`，运行时整体放大 1.67 倍 |

以上路径以本 README 所在的 `Assets/Game/VFX/Combat/Advanced` 为起点。运行时资源库不再调用 `VFX_GroundDustBurst`；不要为了调整扬尘去改它。

## 常用调整

- **火星中心亮点**：直接打开 `VFX_SparksFine`，右边的 `Contact core flash` 是独立系统。黑板 `CoreFlashDuration` 默认 0.1 秒、`CoreFlashSize` 默认 0.24 米、`CoreFlashHDR` 调颜色和亮度。它停在碰撞原点，原有火星运动不受影响。
- **爆炸**：每个 Explosion Graph 内部有火球、`Energetic streak spray` 长条火星、`Ballistic fragments` 碎片；大爆炸还有 `Rising black smoke`。数量、速度、寿命、Scale、HDR 颜色均直接改各自 Initialize/Output。大爆炸的溅射强于小爆炸，不额外调用普通接触 Spark。
- **死亡时序**：资源库 `smallBlastPositions` 是七个模型表面爆点，`smallBlastSpacing` 控制间隔；`smallBlastTail` 是最后一个小爆炸需要留出的播放时间；`finalBlastDelay` 是大爆炸最早发生时刻。代码保证最后一个小爆炸播放完，再开始大爆炸。大爆炸默认选择朝向玩家的外壳表面，再向外偏移 `finalSurfaceOutset`（默认 0.55 米），触发后不追随镜头。爆点的表面取样记录在项目根 `AssetStaging/VFXPhase2/surface_explosion_mounts.json`。
- **风锥**：继续修改原“风锥”Prefab 的材质。Boss 资源库 `chargeWindFollowDrillTip` 默认开启；`chargeWindTipOffset` 为 Boss 本地坐标下的尖端偏移，默认零；`chargeWindScale` 控制整体尺度，`chargeWindRepeat` 控制重复间隔。嵌套外壳和内壳的尖端已对齐；运行时每帧把尖端对准钻尖，而非只把 Prefab 原点放到钻尖。
- **履带尘**：资源库 `dustParticlesPerMetre`、`maximumTrackDustRate`、`trackDustPositions`。按实际水平位移发射，受阻或停止时不继续起尘。
- **大爆炸扬尘**：资源库 `groundDustInstances` 控制同时复用多少份旧扬尘，`groundDustScale` 控制整体尺寸。内部嵌套原 Heavy 并关闭另外两层，原 Heavy 扬尘参数后续修改会自动继承。
- **爆炸火光**：火球的 VFX Output 使用 HDR Color Over Life，配合场景 Bloom 发光。每个 Explosion Prefab 下的 `Orange fire illumination` 有一个无阴影 Point Light；它的 `VFXLightPulse` 调峰值和淡出时长，Light 组件调颜色和范围。
- **能量释放范围**：资源库 `finalRadius` 默认 4.5 米，`crystalRadius` 默认 2 米；对应的 Distortion/Chromatic 参数单位为像素。将两者都设零，效果完全透明。
- **拳套能量**：资源库 `chargingEnergyColor` 是蓄力颜色、`fullEnergyColor` 是满蓄力金色；单个 `WeaponEnergyVFXController` 可开启 `overrideLibraryColors` 覆盖。闪光插槽在拳套 `GauntletViewModel.prefab` 的 `Gauntlet_Mechanism/FX_FullChargeCore`，定位于顶部圆球表面；红闪对象直接作为插槽子物体，粒子采用 Local 空间，出生后仍继承拳套的位移与旋转。可以直接移动这个插槽，或使用资源库 `fullChargeFlashOffset`（米）微调。满蓄力只闪一次红色横向 Dot，保持蓄力不重复刷新红闪。能量覆盖层沿用原蒙皮骨骼，原 PBR 材质保留。

## 预览与实现边界

不进入 Play 也可以查看：**Tools → Combat VFX → Effect Preview**。选中任意特效 Prefab，窗口会播放临时副本；支持重播、暂停、前进一帧、循环周期和速度，拖动画面旋转，滚轮缩放。也可以右键 Prefab → **Preview Combat Effect**。一次性 GPU 特效的静态缩略图可能为空，这不代表资源丢失。

GPU 默认走 Unity 原生 Graph 预览，使用其自带播放/暂停，查看 Graph 默认参数；上方组合预览按钮不控制这一原生面板。复合 Prefab 的完整效果继续使用下方试玩预览场景。自建离屏 GPU 预览目前存在模拟不推进的问题，尚未验收，不把灯光亮起当作粒子预览成功。

点 **编辑 Prefab** 调组件；修改后点 **重新读取**，会优先复制当前 Prefab 模式中尚未保存的调整。点 **选择 Graph** 调 GPU 节点和黑板参数。Graph / 材质是共享资源，修改可直接反映到预览。预览播放临时副本，保留原有 Graph 参数覆盖，游戏资源配置不变。测试地面和背景不会保存进资产。

**拳套材质命中**：Tools → Combat VFX → **Select fist impact settings**，对应 `Resources/GauntletImpactVFX.asset`。钢铁调用原 Spark；混凝土调用缩小的 ConcreteImpactLight 和 POM 裂纹，裂纹直径默认 0.68 米（SlamImpact 3.4 米的五分之一），深度 0.04 米；共鸣者调用 `GauntletBloodHit` 和 `GauntletCrystalHit`。两个小型喷溅 Prefab 继承原紫血/晶屑，降低单次数量，源 Prefab 没有被重置。已有 SurfaceAudio 材质分类同时用于声音和特效；可以在碰撞物体上添加 SurfaceAudio 明确标记 Metal / Concrete / Flesh 等。

**落石和水晶着地**：复用拳套落地冲击的 `ConcreteImpactHeavy.prefab`，在首次地面接触处产生一次。Boss Tuning 中 `Falling debris landing VFX` 可覆盖资源，三个 Scale 分别调普通落石、普通水晶、中央大水晶（默认 0.65 / 0.85 / 1.67）。落地声音和震动沿用原配置。

两个喷气 Prefab 中残留的白色 Cube 及其碰撞体已移除；原烟雾和热浪保留。

**晶柱连锁破碎**：四根晶柱一起消耗，但只有实际被撞的一根产生晶屑烟尾、局部色差冲击波和破碎音，其他三根仅隐藏晶体。紫光使用被撞特效内原有灯光颜色/峰值，约 0.04 秒亮起、0.65 秒内淡出；资源库 `pillarFlashAttack` / `pillarFlashDuration` 可调。未改动晶柱再生、形态切换或充能逻辑。

打开 `Preview/AdvancedCombatVFXReview.unity`，播放后数字键 1–5 选择爆炸、晶体、风锥、热浪、拳套；空格触发，P 暂停。自动轮播可在 `Advanced VFX review controls` 关闭。

大量火星、溅射、碎石、晶体烟尾、移动尘由 VFX Graph 计算。色差/热浪的 GPU SceneColor 分支在当前项目的隔离渲染检查中没有通过，默认使用已验证的普通网格 Shader Graph 折射核心，避免启用不能正确显示的分支。该选择不会把大量碎片改回 CPU。

屏幕折射采样 URP Opaque Texture；透明烟雾不会作为被折射背景。PC 管线的 Opaque Texture 已使用全分辨率。没有使用全屏后处理色差替代局部球体。

爆炸序列图来自用户提供的特效纹理文件夹，使用原生 4K atlas，没有升到 8K。新贴图和 Graph 均在本目录，原 ConcreteImpactHeavy/Light 和用户调整过的 Spark 节点没有被统一重置。

