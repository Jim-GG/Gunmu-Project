# 战斗特效修改入口

- `Sparks.prefab`：火星。双击进入 Prefab 模式，调整 Particle System 的 Start Lifetime、Start Speed、Start Size、Start Color、Emission/Bursts、Shape，以及 Renderer 的材质。代码不会重写这些参数。
- `Explosion.prefab`：爆炸。同样直接编辑粒子系统；可添加子粒子、烟尘、灯光等表现。默认无 AudioSource，避免与独立音效事件叠音。
- `ResonanceShockwave.prefab`：冲击波。Line Renderer 提供材质，UDK7MShockwave 的 Radius Over Lifetime、Width Over Lifetime、Color Over Lifetime 控制扩张、厚度和消散。Preview Radius/Duration 用于单独拖入场景后 Play 预览。Boss 运行时传入实际半径和时长。

火星与爆炸的全局映射在 `Assets/Game/Shared/Actions/Resources/CombatVFX.asset`。可直接替换 Prefab，并修改 Lifetime（对象池回收时间，应覆盖最长粒子寿命）。Hitbox 自带 Contact Effect 时仍优先使用单招配置。

冲击波与聚能效果在场景 `UDK7M Boss Encounter` 的 UDK7MFeedback 上，分别是 Shockwave Prefab / Energy Gather Prefab。可替换为自己的粒子 Prefab；不带 UDK7MShockwave 组件的 Prefab 按自己的粒子表现播放。特效外观不改变命中判定、伤害或击退范围。

其他旧的紫血、碎晶、灰尘等仍由 CombatEffects.cs 的默认粒子池生成，之后也可逐项加入 CombatVFX.asset 替换。无需编辑它们即可修改这里的三个主要效果。
