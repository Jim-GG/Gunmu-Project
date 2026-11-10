# Wind Cone FX · Unity 6.3 / URP

目标工程：棍木项目，Unity 6000.3.26f1，URP 17.3.0。

## 使用

- `Prefabs/WindCone_ProjectAdapter.prefab` 接入棍木项目原有 `GameActions.DashWindConeController`。原来的 `PlayDash(point, forward, intensity)` 调用保持适用；战斗特效库的 `windCone` 指向此预制体。原引用保存在 `Provenance/OriginalProjectWindCone.json`，旧预制体仍在原目录。
- `Prefabs/WindCone_Burst.prefab` 是可独立调用版本，使用 `WindConeFX.WindConeBurst.PlayDash(point, forward, strength)`。可重复播放，生命周期由 MaterialPropertyBlock 控制，不为每次播放创建材质。
- `Prefabs/WindCone_Loop.prefab` 持续循环，用于调色、检查滚动方向。
- `Scenes/WindCone_Demo.unity` 是独立演示场景，播放后循环风锥。角色大小参照为 1.8 米胶囊。
- 朝向约定：根对象本地 **+Z 为前进方向**。外壳从后方展开并向前收窄，位置参数表示角色中心附近；按角色中心调整 Y，高矮不同可缩放预制体或调用强度参数。

## 可调参数

主贴图是打包数据，默认 **G 通道**作烟雾遮罩，不能直接把 RGB 当作风颜色。两张贴图使用 Linear、Repeat。材质 `WindCone_Outer/Inner/Wake` 可分别调节色调、发光、透明度、UV 滚动、扭曲、破碎阈值和两端淡出。

Shader 使用：

`遮罩 = G(烟雾纹理(原UV × 平铺 + 时间 × 速度 + 噪声扭曲))`

`透明度 = 破碎后的遮罩 × 原UV轴向淡出 × 生命周期 × 颜色透明度`

淡出基于未滚动 UV，避免滚动时整个锥体两端突然闪烁。混合为预乘透明，可用 `_Additive` 连续调到加法；两种模式都乘生命周期，防止加法模式结束后残留。

## 来源和重建边界

`SourceMeshes` 为本地绝区零发行 Mesh 数据；运行网格做了明确的 X 镜像和三角绕序转换，UV 原样保留。`Textures` 的 `Eff_Smoke_075`、`Eff_Noise_006` 是实际提取贴图。壳体组合、尺寸、播放时序、URP Shader 源码和控制器是本次重建，并非原游戏源工程或逐像素复现。

原材质/Shader 依赖、源文件哈希和粒子 Renderer 候选关系见 `Provenance/SourceCaseManifest.json`；原 Shader 编译数据和完整研究保存在绝区零资产工作区，不把 500MB 审计文件塞入游戏工程。此实现不需要 VFX Graph，也不依赖屏幕颜色或深度纹理。

编辑器菜单 `Tools/Wind Cone/Build and validate` 用源 JSON 生成网格、材质、预制体及示例；验证会隔离渲染生命周期开始、中段、结束并记录结果。例图及 JSON 位于项目根目录 `WindConeValidation`。
