# Modern Lab

14 个共享材质：原有实验室表面，加上横向分层白墙和白漆金属。使用 `ModernLab/ModernLabLayered` Shader Graph；共用 33 张 2K 主贴图与 2 张 1K 微表面法线。

- `Prefabs`：29 个原始模块与 7 个派生模块，共 36 个已连接材质的预制体。
- `Materials`：共享分层 Lit 材质；`Preview_*` 仅用于展示。
- `Textures`：Mask = R Metallic / G AO / B Dust / A 清洁底材 Smoothness；BaseColor Alpha 为黑污点遮罩，表面保持不透明。Normal 为 +Y。彩色漆复用白漆贴图。
- `Scenes/ModernLabModuleReview.unity`：实际楼梯和九种金属护栏的展示。
- `Scenes/ModernLabAdditionalSurfaces.unity`：棋盘格、吊顶、圆弧波纹墙与彩色色带墙。

UV0 以 2 米为一个贴图周期。给其他模型使用时需保持这个尺度。正式关卡请捕捉关卡自己的 Reflection Probe，地面反射会随环境变化。

在材质 Inspector 调整：

| 参数 | 用途 |
| --- | --- |
| `Paint Tint` | 漆膜颜色；中性污点、灰尘不随漆膜染色 |
| `DustAmount` | 薄灰强度；0 关闭。主要改变反射和粗糙度 |
| `DustColourStrength` | 灰尘对底色的影响；0 仅保留材质响应变化 |
| `DustRoughness` | 覆灰区域的目标粗糙度 |
| `DustTiling` | 仅灰尘遮罩的重复次数；数值越大，形状越细小 |
| `SpotAmount` | 黑污点强度，与灰尘独立 |
| `NormalStrength` / `MicroStrength` | 主法线 / 微表面法线强度 |
| `MicroTiling` | 微表面细节的重复次数 |
| `RoughnessBias` | 增大使底材更粗糙，减小使底材更光滑 |
| `SurfaceTiling` | 整体主贴图尺度，连同板缝与砖缝一起改变 |

灰尘与其他细节均使用模型 UV，不使用世界坐标。粗墙漆、分层白墙、波纹墙、吊顶的生成预设默认关闭灰尘，其余表面使用较轻的强度。Inspector 中的手工调整独立保存于各个材质。

分区墙的白色与彩色色带为两个独立材质槽。棋盘格默认 50 厘米；`SurfaceTiling` 为 2 时变为 25 厘米。

Blender 源工程、材质库、回退副本和完整说明位于项目根目录的 `ArtSource/ModernLab`。重新准备资产的菜单为 `Tools → Modern Lab → Prepare Materials And Module Prefabs`，会从 `surface_v2_unity.json` 恢复生成预设并覆盖手调参数；仅调材质时不需要运行它。长期保留自定义版本可复制材质，或同步修改该配置。当前用户手调值不再自动覆盖。
