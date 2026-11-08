# MCU-7A 无人机资产

游戏入口：`Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab`。拖入场景即可使用；独立展示场景为 `Assets/Game/Scene/MCU7A_Drone_Review.unity`。

## 当前版本：v13，同为 4K 的清晰度重烘

没有增加贴图分辨率、数量或材质槽。重新按用途分配 UV：文字 / 图标区域的线性像素密度约为 v12 的 3 倍，旋翼边框标记约 2.63 倍，紧固件 / 警示约 1.68 倍。旋翼顶环拆成 8 个四分之一圆条带，回收原先空置的圆心区域；内部面、背面、纯色面和使用独立材质的玻璃面压缩。尾翼黑色装饰也单独保留纹理预算。

UV 几何占用约由 48.6% 提升到 68.3%。完整重烘颜色、法线、材质参数和发光，使新的切线空间与 UV 一致；保留 mipmap 和原有 BC7 / BC5 压缩。Blender 采用 32 samples，填充边距为 8 像素。没有使用 8K 中间图缩小、后期锐化或取消 mipmap 来制造清晰度。

当前 Blender 文件在 **`ArtSource/Drone/UserModel/Baked_v13/`**：`Drone_Bake_Workspace.blend`、`Drone_Baked_Review.blend`、`MCU7A_GameReady.blend`。v12 完整保留；替换前的 Unity FBX、贴图及其 `.meta` 在 `Baked_v13/PreviousUnity/`。回退时 FBX 与四张贴图必须成套恢复，因为 UV 布局已变更。

指标在 `Baked_v13/uv_repack_report.json`，UV 范围与重叠采样检查在 `uv_audit.json`；相同相机 / 灯光的前后近景在 `Baked_v13/Preview/`。像素密度提升是各类区域按面积汇总的几何指标，并不表示整个模型的每处都统一提高三倍。代价是无字纯色面和隐藏面分配到的纹理空间较少，微表面细节仍由已有共享微法线提供。

## 本次交付

- Unity 中共 **12,407 个三角面、3 个 MeshRenderer、2 个共享材质**。作者版烘焙源约 57,510 个三角面。机身包含玻璃子网格，两个旋翼独立，因此每架通常有 4 个不透明材质批次，另计阴影等渲染通道。
- 白色釉面、黑塑料、钛金属与发光使用同一组唯一 UV。贴纸、四种功能图标、文字、紧固件和浅槽烘焙到纹理；大盖板、卡扣、散热叶片、镜头与转子保留几何。平面图案不参与法线烘焙，避免反向法线造成黑斑。
- 4 张 4096 贴图：BaseColor、切线法线、Mask、Emission。Windows 导入采用 BC7 / BC5、mipmap 与纹理流送。所有无人机实例共享贴图和材质，不会各自复制一套。
- Mask：R 金属度、G AO、B 灰尘、A 光滑度。本版 AO=1、灰尘=0；并未烘焙独立 AO。
- Emission：RGB 发光颜色乘 8，A 是清漆权重。颜色与发光纹理为 sRGB，法线及 Mask 为线性数据。没有将照明或反射烘死在颜色图中。
- 新材质沿用 ModernLab 微法线的 RNM 混合方式，并共用 `Lab_Detail_Coating_Normal.png`。新增无人机专用 URP 材质以支持清漆和发光，未改动原有共享 Shader Graph。
- 最上方辅助镜头是暖黄色发光面，并带真实 Spot Light；其余镜片使用蓝绿色光学玻璃近似材质，外圈为钛金属。Unity 实时玻璃没有复现 Cycles 的透射和折射，最终高光也受关卡灯光与反射探针影响。

## Blender 文件

原始 `ArtSource/Drone/UserModel/UserDrone_Enamel_Detail_v11.blend` 未覆盖。以下是保留的 v12 文件；当前游戏源文件请使用上方 v13 路径。

- `ArtSource/Drone/UserModel/Baked_v12/Drone_Bake_Workspace.blend`：修正过贴合的高模源与完整 UV 的接收网格，适合继续修改、重烘。
- `ArtSource/Drone/UserModel/Baked_v12/Drone_Baked_Review.blend`：可对比高模与烘焙版，包含摄影棚、相机和渲染材质。
- `ArtSource/Drone/UserModel/Baked_v12/MCU7A_GameReady.blend`：清理后的游戏模型，保留机身、左右旋翼和必要材质，贴图已打包。

游戏模型约宽 4.09 米；FBX 导入后前向为 -Z，预制体将视觉模型转了 180°，因此预制体前方为 Unity +Z。运行时旋翼引用指向独立、朝上的旋转支点，避免导入轴转换导致旋翼竖着转。

## 使用与调整

1. 放入 `Drone_MCU7A.prefab`。在 `TraversalDrone` 上设置 Route、Start / End Anchor、Speed、Phase Offset。与原无人机使用相同路线逻辑。
2. `Load-bearing collision deck` 是承重 BoxCollider，可调整 `Center / Size`。本版按中央盖板设置为 0.82 × 1.12 米，原路线中的承重顶面高度保持不变。相比旧占位模型，中央落脚区更窄；旋翼不是站立碰撞面。
3. `Drone_Atlas_Enamel.mat` 可调整 Normal strength、Micro normal strength、Glazed enamel coat multiplier、Emission multiplier。微法线使用 UV，随物体移动，不使用世界坐标灰尘。
4. 补光灯的色温、亮度、距离可在视觉模型下的 `Warm yellow fill lamp` 调整；下降循环消失期间，灯光会一同关闭。

旧 `TraversalDronePlaceholder.prefab` 保留 GUID，替换内部外观；V1 场景中的独立实例也已升级。V2 和可拖拽的下降路线模块继承更新后的预制体，锚点、速度与相位保留。关卡生成器优先使用成品预制体。

## 重新导出

脚本在 `ArtSource/Drone/UserModel/Tools/`：依次运行 `prepare_bake_v12.py`、`bake_atlas_v12.py`、`export_game_v12.py`；用 Blender 后台分别加载 v11、Bake_Workspace、Baked_Review。默认重烘所有贴图，`--resume-colour` 只用于确认颜色与粗糙度未变化时恢复中断的任务。

v13 流程在 prepare 后增加 `repack_uv_v13.py`，从 v12 接收网格生成 v13 接收网格；然后在 v13 workspace 中运行 `bake_atlas_v12.py --output-dir <Baked_v13绝对路径> --samples 32 --bake-margin 8`，在 v13 review 中运行 `export_game_v12.py --output-dir <Baked_v13绝对路径>`。这些参数放在 Blender 命令行的独立 `--` 后。导出后可运行 `finalize_game_v12.py --output-dir <Baked_v13绝对路径>` 清理游戏 Blender 文件。`audit_uv_v13.py` 用于核对 UV 并生成彩色分配图。

Unity 导入脚本是 `Docs/Drone/ImportDrone.cs`，通过现有 Pipeline 的 `script run` 调用 `ImportDrone.Run`；预制体升级调用 `ImportDrone.Integrate`。脚本不放入 Assets，以免成为运行时依赖。重新修改源模型后，需要重烘并重新导出，再运行导入，不能只改 FBX。

仅更新 UV / 贴图时，使用 `Docs/Drone/CaptureUV13.cs` 的 `CaptureUV13.Reimport`，只重导入同路径 FBX 和四张贴图，不重建预制体，也不重设已经调过的材质或玩法参数。

## 验证和回退

- `Docs/Drone/asset_audit.json`：V1、V2 三架下降无人机及预览场景的引用、材质、网格与轴心检查通过；着色器无编译错误。
- `Docs/Drone/live_validation.json`：实际 Play Mode 中无人机与玩家均移动约 1.20 米，水平载人误差为 0；转子旋转、下降循环隐藏 / 重生、补光灯开关均通过，没有记录到运行时错误。
- `ArtSource/Drone/UserModel/Baked_v12/game_blend_audit.json`：游戏 Blender 文件的对象、材质、UV 范围与零面积面检查。
- 原占位预制体与场景快照在 `ArtSource/Drone/UserModel/Baked_v12/UnityBackups/`。恢复旧外观需同时恢复原预制体和对应场景；不要重新生成 `.meta`。

未进行整款游戏的独立构建或目标硬件性能基准；本轮验证范围是 Unity 编辑器中的导入、实际渲染与 Play Mode 行为。
