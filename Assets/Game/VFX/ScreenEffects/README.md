# 可复用战斗屏幕效果

`ScreenEffectsController` 自动挂到原版拳套玩家的游戏相机。关卡和 UDK7M 使用同一入口；无需给每次冲击生成新的全屏对象。

## 两个配置资源

- `Assets/Game/VFX/ScreenEffects/Resources/CombatScreenEffects.asset`：事件预设、8/12/16 次径向采样、高速阈值、蓄力暗角、共鸣紫色噪波、低血量红边。
- `Assets/Game/Shared/Actions/Resources/CombatFeedback.asset`：三级出拳、肉体打击、主角受击、共鸣者吼叫、尸体落地声音；卡肉时长；落石/水晶尺寸；电流覆盖材质和时长；半透明警示圈及余量。格挡恢复原有 `ObjectAudio.ParryImpact`，自定义 guardImpact 留空。

事件表可以独立调径向强度和范围、快速起峰/衰减、色散像素、紫色噪波透明度、镜头推拉幅度与频率。没有事件、低速、健康且未蓄力时跳过整个 Pass。

## 玩法事件

| 入口 | 表现 |
| --- | --- |
| 轻拳 / 一级 / 二级 / 三级真实命中共鸣者 | 四档径向冲击、轻微快速镜头推拉、局部卡肉 |
| 高速移动 | 速度 10–28 米/秒逐渐增强径向模糊，减速后恢复 |
| UDK7M 砸击、撞击、落物冲击 | 随距离衰减的短促径向脉冲 |
| 通用爆炸、BOSS 连续小爆炸与最终爆炸 | 有上限的短脉冲，不永久保持高模糊 |
| 钻头吞噬尸体引发共鸣爆炸 | 附近相机的径向冲击、色散、紫色透明流动边缘噪波 |
| 共鸣者吼叫 | 默认 18 米内的轻量径向模糊与色散，随距离衰减 |
| 撞晶柱 | 0.6 秒模型电流覆层、世界色散能量冲击及附近相机脉冲 |
| 实际受伤 | 根据来源方向轻微抬头，左右来源附加反向偏转 |
| 三档蓄力 | 轻量递增暗角，最大额外暗角默认 0.12 |
| 低于 30% 生命 | 半透明不规则红边，生命恢复后消失 |

径向模糊与色散、暗角、噪波是独立参数。事件取最大模糊/色散/噪波而非无限叠加强度，最多保留 16 个脉冲；镜头位移与角度也有上限。UI 的 Overlay Canvas / IMGUI 在全屏 Pass 之后绘制。

## 从新机关调用

给机关添加 `ScreenEffectEmitter`，选择 Cue、范围、强度，通过 UnityEvent 调用 `Emit()`。也可以在代码中发送：

```csharp
// 世界事件：距离超出 24 米的相机不会响应。
ScreenEffectSignals.Emit(ScreenCue.Explosion, transform.position, 1, 24);
// 个人事件：只有指定玩家接收。
ScreenEffectSignals.Emit(ScreenCue.ChargeTwoHit, hitPoint, receiver: player);
```

`RadialBlurController` 提供 `PlayAtWorldPosition`、`PlayAtViewportPosition`、`PlayAtScreenCenter` 和 `Stop`。世界点在背后或屏幕外时回退到屏幕中心。相机组件的 Context Menu 提供中心冲击、共鸣爆炸和停止预览。`forcePassForPreview` 仅用于检查零强度透传，正常游戏关闭。

`Stop()` 清除瞬时脉冲；速度、蓄力和生命仍随玩法重新采样。暂停期间不播放冲击或镜头反馈。

## 时间与物理

卡肉使用真实秒数：轻拳/一级 0.033，二级 0.05，三级 0.078。只暂停玩家挥拳/平移和活体敌人受击动作。绝不修改 `Time.timeScale`，也不把布娃娃设为运动学，因此三级击飞继续模拟。

非致命的三档击退写入 `GauntletActions.asset` 与 UDK7M 玩家实际引用的 `ParkourGauntletActions.asset` 的 HitWindow，分别为 0.78 / 0.91 / 1.67 米，沿现有碰撞和导航约束移动；遇墙会缩短实际位移。三级保留原版即时致死和布娃娃发射。`CombatFeedback.asset` 的距离是配置工具的输入；再次运行“Tools / 晶振 / 接入战斗音画与屏幕效果”会将其写入两份动作数据。

落石使用新增四块 mesh，普通水晶在 `Crystal_07_Fractured` / `Crystal_04_Squat` 间选取，中央水晶使用 Squat。按最大边长归一化：落石 2.1 米、普通水晶 1.3 米、中央水晶 3.1 米。显示与凸包碰撞共用 mesh、位置、缩放和旋转，旧方盒碰撞禁用。动态刚体使用凸包，凹陷部分会被物理凸包填平。警示圈按候选落物所有旋转的最大包围范围加余量计算，使用独立半透明材质，不产生碰撞。

导入工具按共面三角形累计面积，为每块石头烘焙“最大面朝下”的旋转。运行时复用该姿态，叠加随机水平朝向与默认最多 6° 倾斜，仅绕世界竖直轴慢转，不再任意翻滚成细边着地。无需修改 Blender 原模型，也不需要为变体复制 mesh。

共鸣者在真实 `Screaming` 状态播放新吼叫，带重播间隔和空间衰减。主角受击声音由真实扣血触发，完美格挡不播放受伤声；同一时间多次伤害有音效限流。紫色血雾使用现有 8×8 烟雾图集的逐帧动画，每次只生成 4–5 粒，不把整张图集画在单粒上。

## 渲染方式与材质归属

Unity 6000.3.26f1 / URP 17.3，Render Graph 兼容模式关闭。给既有 PC / Mobile Renderer **追加** Unity 自带 `FullScreenPassRendererFeature`，注入 After Rendering Post Processing，Fetch Color Buffer 开启、Requirements=None。保留原有 SSAO、Decal、渲染器及 Volume 参数。

全部径向采样从同一份 `_BlitTexture` 快照读取，使用 SRP `Blit.hlsl` 与 Clamp 采样。默认 12 次，8/16 次为编译期变体。全屏色散和额外暗角在本 Pass 单独控制，因此不需要运行时改写团队共用 Volume Profile；原有 Volume 效果仍先正常渲染。

每个相机实例化自己的 Material。只在该相机的渲染回调中绑定到原生 Feature，结束即解除；不用全局 Shader 属性。材质资源默认零强度，退出播放不会把脉冲写回资源。镜头反馈也只在该相机渲染期间临时叠加，随后还原，避免与原版骨骼镜头或关卡镜头累计漂移。

使用的 Unity 官方参考：[Full Screen Pass](https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass)。用户提供的 Radial Blur 文档作为实现参考；实际范围以本次玩法需求为准。

## 验证入口

UDK7M 三阶段场地专项：`AssetStaging/CombatPolish/Validate.request`。先编译后创建请求，报告输出 `Docs/Integration/combat-polish-validation.json`，同机位对照图位于 `AssetStaging/CombatPolish/`。验证在场地临时副本中进行，不保存或覆盖正在编辑的场景。

首版目标为 PC 单 Gameplay Camera。Mobile Renderer 已接入，移动硬件、XR、动态分辨率和多玩家相机尚未实机验证。GPU 毫秒耗时需要目标设备 GPU Profiler 进一步测量；自动 Camera.Render 截图不作为 GPU 性能数字。
