# 现代实验室材质与模块包

## V2 当前版本（2026-11-05）

当前已实现 14 个分层材质、33 张 2K 主贴图与 2 张共享 1K 微法线。Unity 使用 `ModernLab/ModernLabLayered` Shader Graph；Blender 成品和材质库已保存对应的节点与打包贴图。下方 V1 参数表与验证记录保留作历史参考，当前参数以材质 Inspector 和 `surface_v2_report.json` 为准。

灰尘改为细碎、低对比的 UV 遮罩，主要影响反射。粗墙漆、分层白墙、波纹墙和吊顶的生成预设关闭灰尘。`DustAmount`、`DustColourStrength`、`DustRoughness`、`DustTiling` 独立可调；`DustTiling` 越大，灰尘图形越小，且不会改变结构接缝尺度。污点、主法线与微法线均可独立控制。完整参数说明见 `Assets/Game/Environment/ModernLab/README.md`。

新增 `Lab_White_Coursed` 横向分层白墙、`Lab_Paint_WhiteSteel` 白漆金属。Mask 通道为 R 金属度 / G AO / B 灰尘 / A 清洁底材光滑度；BaseColor Alpha 为黑污点遮罩。着色保持不透明，所有表面细节使用模型 UV。

V2 贴图与节点作者脚本为 `Tools/revise_surfaces_v2.py`，从已有成品运行。`Tools/build_materials.py` 是 V1 全量生成器，单独运行会恢复旧版本；需要重建 V2 时必须再运行 V2 作者脚本。生成/重新准备材质会重置手调参数，日常调材质不必重建。用户目前的 Inspector 手调值予以保留。

V1 回退文件与 Unity 资产压缩包在 `Backups/V1_BeforeSurfaceRevision`。V2 近景位于 `Preview/Surface_V2_*.png`，为 Blender 离线渲染，Unity 光照下表现以编辑器为准。

## V1 历史记录

现代实验设施材质与模块：保留原始 29 个模块，新增 7 个派生模块，共 36 个 Unity 预制体。12 个共享材质使用 27 张 2K 贴图，彩色漆直接复用白漆贴图并用颜色参数调色。

## 文件

- `ModernLab_ModularKit.blend`：完整成品。`Modular Asset Kit` 保留原始布局，`Additional Modules` 为派生模块；`Module Review` 展示实际楼梯护栏，`Additional Surface Review` 展示棋盘格、吊顶、波纹墙和分区墙。
- `ModernLab_Materials.blend`：可追加的 12 材质库，贴图已打包，材质已标记为资产，分类为 `Modern Lab/Surfaces`。
- `Source/ModularKit_InputSnapshot.blend`：修改前的输入快照。
- `Source/BeforeMaterialApply.blend`：在当前 Blender 窗口打开成品前，再次保存的副本。
- `Source/ModernLab_ModularKit_BeforeMetalFix.blend`：最初三材质版本的回退副本。
- `Source/BeforeFinalMaterialLoad.blend`：打开最终成品前，再次保存的 Blender 窗口副本。
- `Preview/ModernLab_ActualHandrail.png`：实际扶手模型的 Blender 近景。
- `Preview/ModernLab_ActualModules.png`、`ModernLab_UnityActualModules.png`：实际楼梯护栏的 Blender / Unity 渲染。
- `Preview/ModernLab_AdditionalSurfaces.png`、`ModernLab_UnityAdditionalSurfaces.png`：新增材质和分区模块的 Blender / Unity 渲染。
- `build_report.json`：原物体与导出文件的对应关系。
- `validation.json`：源网格和打包贴图检查。

Blender 源文件在 Unity 的 `Assets` 目录之外；Unity 使用导出的 FBX 和 PNG，不会触发自动 `.blend` 导入。

## 共享材质

| 名称 | 用途 | 平均粗糙度 |
| --- | --- | --- |
| `Lab_White_Matte` | 哑光白色墙板、楼梯立面 | 0.846 |
| `Lab_Dark_Panel` | 深灰功能墙板 | 0.682 |
| `Lab_Floor_Resin` | 连续树脂地面、楼梯踏面 | 0.180 |
| `Lab_Rail_BrushedSteel` | 全部 9 个金属扶手与护栏 | 0.285 |
| `Lab_Floor_Checker` | 灰白棋盘格，50 厘米方砖 | 约 0.265 |
| `Lab_Ceiling_Acoustic` | 白色细穿孔模块吊顶 | 约 0.839 |
| `Lab_Wall_Ribbed` | 深灰竖向圆弧波纹墙，约 8.3 厘米肋距 | 约 0.601 |
| `Lab_Paint_RoughWhite` | 纯白粗糙墙漆 | 0.880 |
| `Lab_Paint_GlossWhite` | 纯白光滑涂层 | 0.220 |
| `Lab_Paint_Lime` / `Red` | 可调色的绿色 / 红色粗糙漆 | 同粗糙白漆 |
| `Lab_Paint_Yellow` | 可调色的黄色光滑漆 | 同光滑白漆 |

基础表面每套使用三张 2048×2048 无缝贴图：BaseColor、Normal、Mask。彩色变体复用白漆的全部贴图，不为每个模块或每种颜色重复生成贴图。预览中的灯具和地面标线使用展示辅助材质。

Mask 通道：R = Metallic（本套为 0），G = AO，B = 备用，A = Smoothness。Blender 将 A 反转为 Roughness；Unity URP 的 Smoothness 乘数设为 1。法线为切线空间 OpenGL +Y。

## 在 Blender 继续制作

直接打开模块工程即可编辑。原有网格、位置和修改器保留；新增的 `LabUV` 用于材质，原 UV 仍保留。材质读取当前渲染 UV。扶手的 `Lab Surface UV` 几何节点修改器在蒙皮、细分之后生成最终表面的 UV，避免线段控制网格没有面时纹理失效。

在其他工程使用时，选择 File → Append → `ModernLab_Materials.blend` → Material，追加所需材质；也可将本目录添加为 Blender Asset Library，在资产浏览器中拖入材质。

UV 的物理尺度是 1 UV 单位对应 2 米。白墙每 1×2 米一块板，深灰平板每 1×1 米一块板，棋盘格每格 0.5 米。新做的物体需设置对应尺度的 UV。波纹和细穿孔由法线表现，外轮廓保持平整。

在 Blender 调色：找到彩色漆节点中的 `TINT / 主色调`，修改第二个颜色输入。法线、粗糙度继续复用白漆。分区墙的白色与彩色色带已分为两个材质槽，可以独立换材质。

新增派生模块包括 6 米和 3 米吊顶、绿 / 红 / 黄色带墙，以及纯白粗糙 / 光滑样板。它们放在独立的 `Additional Modules` 场景，不覆盖原始模块布局。

## 在 Unity 使用

将 `Assets/Game/Environment/ModernLab/Prefabs` 中的模块拖入场景，材质已连接。`Assets/Game/Environment/ModernLab/Materials` 是三个共享材质的位置。

FBX 的 UV0 已固定为 `LabUV`，使用导出时求值后的修改器结果。薄墙、色带墙和吊顶的导出副本补充了相反朝向的背面，配合背面剔除使正反两侧都正常受光。原始可编辑网格保留在 Blender。FBX 不含场景灯光和摄像机。

优先打开 `Assets/Game/Environment/ModernLab/Scenes/ModernLabModuleReview.unity` 查看实际楼梯护栏；打开 `ModernLabAdditionalSurfaces.unity` 查看新增表面与色带。最初的基础几何体展示仍保留为 `ModernLabMaterialPreview.unity`。预览使用独立光照、颜色配置和保存的房间反射 cubemap；位于 Layer 30，摄像机、灯和 Volume 均已配置。

在 Unity 调色：复制或选择 `Lab_Paint_Lime`、`Red`、`Yellow`，直接修改材质 Inspector 的 Base Map 颜色。要换粗糙 / 光滑响应，可以替换为对应的白漆材质再改颜色。分区墙的色带位于第二个材质槽，白色部分位于第一个槽。棋盘格的 Tiling 设为 2 可变为 25 厘米方格，设为 0.5 可变为 1 米方格。

正式关卡的反光地面需要该房间自己的 Reflection Probe；移动布局后重新捕捉房间反射。展示房间的 cubemap 仅用于展示房间。Blender 的 Cycles 与 Unity 的实时光照不同，亮度和反射会随场景灯光变化。

Unity 菜单 `Tools → Modern Lab → Prepare Materials And Module Prefabs` 可重新连接贴图、材质及预制体，并重新生成展示场景。该操作会更新本套生成的资产；手工修改后先保存副本。

## 重建与验证

`Tools/build_materials.py` 从输入快照生成贴图、材质、UV 和 FBX；`Tools/finalize_blender.py` 检查并保存 Blender 成品及展示图。脚本中的根目录对应当前项目。

Unity 侧检查与结果位于 `Docs/ModernLab/Validation`。包含三材质和九贴图通道、29 个模块共享引用、UV0 尺度、切线、实际渲染及预览关闭时的错误检查。预览反射通过独立相机捕捉，避免本版本 URP 下原生探针烘焙得到空背景的问题。

当前项目的 PC URP 反射探针图集路径在 Scene 视图重绘时会触发 `ReflectionProbeManager.UpdateGpuData` 空引用。已将 `Assets/Game/Settings/PC_RPAsset.asset` 的 Reflection Probe Atlas 关闭；保留普通探针反射和 Box Projection。Forward+ 下的图集探针混合也随之关闭。如果之后升级管线并确认问题消失，可在该管线资产的 Inspector 中重新启用 Reflection Probe Atlas。未改动战斗场景中的探针参数；验证报告 `EditorRenderingReport.json` 记录了关闭图集后的连续重绘检查。
