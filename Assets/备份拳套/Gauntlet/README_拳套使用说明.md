# 拳套 / Blender → Unity

本目录是从当前打开的 `Parry.blend`（含尚未保存的编辑状态）移植的 Unity 资源。原 F 盘工程未被覆盖。

## 先看效果

打开 `Assets/Gauntlet/Scenes/GauntletPreview.unity`，进入 Play Mode。

- `1 / 2 / 3 / 4`：播放对应的 punch 动作。
- `C`：charge；`I`：Idle。
- `P / K / H / O`：Parry-Start / Parry-Keep / Parry-Hit / Parry-Holster。
- 左侧按钮可播放所有 13 个动作，包括三条丢弃动作。

预览场景只是检查模型、材质和动作的独立场景，不会替换现有关卡。非循环动作播完后返回 Idle；Idle、Parry-Keep 保持循环。预览按钮不包含伤害、攻击判定或连招规则。

## 放进玩家相机

1. 将 `Prefabs/GauntletViewModel.prefab` 拖到玩家 Camera 下。
2. 把预制体根节点的局部位置和旋转设为 0，缩放设为 1。
3. 相机 Near Clip 建议为 `0.015–0.02` 米；源工程的 16:9 画面对应垂直 FOV 约 `58.7155°`。更大的 FOV 会让手臂显得更小。
4. 也可以选中场景中的 Camera，使用 `Tools > Gauntlet > Attach to Selected Camera`；该操作支持 Undo。

`GauntletViewModel` 已经校准了 Blender 原相机的朝向和眼睛位置，不需要额外旋转 90° 或缩放 100 倍。保留 `Gauntlet_Model` 子节点上的校正变换。新增的 `ViewCameraMotion` 骨骼逐帧记录了每个动作中原 Camera 经过复制旋转等约束后的最终位置和旋转；`GauntletCameraMotion` 在 LateUpdate 将这些偏移传给父相机，相当于 Unity 中的跟随约束。出拳约 3° 的镜头运动也在其中。关闭 Apply Camera Motion 可取消。

推荐层级是 `Player → CameraPivot（鼠标转向）→ Camera（动作偏移）→ GauntletViewModel`。控制器会对手臂模型做反向补偿，避免相机动画在手臂上叠加两次；停用或卸下拳套时恢复相机原局部位置和旋转。参考相机姿态来自 Idle 第 0 帧。源工程没有相机镜头数据的 Action，因此此处同步骨骼驱动的位置 / 旋转，FOV 使用源工程固定值。

在现有战斗脚本中引用 `GauntletAnimationPlayer`，调用：

```csharp
gauntlet.PlayClip("charge");
gauntlet.PlayClip("punch1");
gauntlet.PlayClip("Parry-Start");
gauntlet.PlayClip("Parry-Keep");
gauntlet.PlayClip("Parry-Hit");
gauntlet.PlayClip("Parry-Holster");
gauntlet.SetCharge(0.75f);
```

`PlayClip` 提供基础播放和回待机。若后续战斗系统需要精确的格挡窗口、蓄力释放或连招衔接，应由战斗状态机决定切换时机；动画素材已分别保留。

## 目录和导入设置

| 路径 | 用途 |
|---|---|
| `Models/Gauntlet_Model.fbx` | 一套骨架、一个蒙皮网格、两个子网格；不夹带相机和灯光 |
| `Animations/Gauntlet_*.fbx` | 13 条独立的骨骼动画，只含骨架和必要层级 |
| `Animations/Gauntlet.controller` | 所有动作状态；默认 Idle |
| `Animations/Clips.json` | 源动作名称、帧范围、30 FPS 和对应 FBX |
| `Materials/` | 不透明图集材质、透明玻璃材质和 URP 图集着色器 |
| `Textures/` | 4 张合并贴图 |
| `Prefabs/` | 相机挂载用预制体 |
| `Editor/GauntletAssetTools.cs` | 可重复的导入、构建、挂载工具 |

Rig 使用 Generic。动画 FBX 共用模型的 Avatar；所有约束已经逐帧烘焙为骨骼变换，不依赖 Blender 的 IK、Child Of、NLA 或动画槽系统。关闭 Root Motion。手臂骨架与拳套机械骨架合并后，加上新增的相机运动骨骼，导出骨架共 62 根骨骼，其中机械骨骼名为 `Gauntlet_Mechanism`。

动画分为：charge（3.1 秒）、Idle（2.333 秒）、4 条 Parry（0.267 / 0.267 / 2.2 / 0.267 秒）、punch1–4（1 / 0.6 / 0.667 / 0.667 秒）和三条丢弃（各 2.333 秒）。三条丢弃是源工程中的三条有效 Action，均予以保留；没有把空 Action 或旧 NLA 的重复条目导出。

## 材质合并

拳套原有 24 个材质槽，加上手套和袖子的材质，合并为：

- **Gauntlet_Atlas**：金属、黄黑涂装、橡胶、布料、灯光与能量内芯，共用一个材质和一套 UV。
- **Gauntlet_Glass**：透明灯管外壳与烟色灯罩，共用一个透明材质。

完全透明的内部接缝面仅在导出副本中移除。备份拳套、控制器形状、参考准星、源灯光和相机不会进入游戏模型。

| 贴图 | 分辨率 | Unity 含义 | Windows 格式 |
|---|---:|---|---|
| Gauntlet_BaseColor | 4096² | 基础颜色，sRGB | BC7 |
| Gauntlet_Normal | 4096² | 切线空间法线，线性 | BC5 |
| Gauntlet_MetallicSmoothness | 2048² | R 金属度，A 光滑度 `1 − roughness`，线性 | BC7 |
| Gauntlet_EmissionRegions | 2048² | RGB 自发光，sRGB；A 区分能量内芯 / 呼吸灯 | BC7 |

这里减少的是材质槽及其主绘制提交；四张贴图分别服务于颜色、法线、金属/光滑和自发光。贴图已使用 Mipmap 和 Windows GPU 压缩，关闭 Read/Write；按压缩格式估算的 GPU 数据含 Mipmap 约 53.3 MiB，编辑器的内存统计还可能包含导入 / CPU 缓存。透明玻璃不额外加载贴图。最终帧的绘制次数还受 URP 其他 Pass 影响。

源模型保留了细节网格。导出时明确使用 Blender 的三角化结果，修复 Unity 对凹多边形重新三角化产生的内侧缺口。上盖靠近冲击块的前缘开口，按用户确认在导出副本中补上封口，并沿用上盖涂装和机械骨骼蒙皮。此轮没有简化机械细节。

Blender 的复杂节点已烘焙成静态 PBR 贴图；能量内芯与呼吸灯由 Unity 着色器提供近似流动 / 呼吸效果，`SetCharge` 可控制能量亮度。Blender 原有的复杂程序纹理动画、真实透射、折射和灯罩吸收不能靠 FBX 自动迁移，Unity 玻璃采用 URP 透明近似。原节点完整保留在源快照中。

## 原工程缺失的图片

源文件引用的下列图片在其原路径不存在：

- `glove_hardknuckle_Roughness.png`
- `sleeve_st6_generalist_Roughness.png`
- `sleeve_st6_generalist_Height.png`
- `v_c_skin.png`
- `v_skin_normal.png`

手套 / 袖子缺失的粗糙度按 `0.58` 处理，缺失高度不生成凹凸；两张皮肤图片对应的材质没有使用中的面。所有已找到的源贴图和程序材质参与了烘焙。后续若找回源图，可用源快照重新烘焙。

## Blender 文件与验证

工程外层的 `ArtSource/Gauntlet/` 中：

- `SourceRevisions/2026-11-02_ElbowFix.blend`：最新肘关节修正版的完整快照，保留原始节点、骨架、备份、控制器与 Action；当前 Unity 导出以此为源。
- `Gauntlet_SourceSnapshot.blend`：首次移植时的原始快照，用于追溯。
- `Gauntlet_UnityReady.blend`：整理后的自包含导出版，只有 `GauntletAsset → GauntletRig → Gauntlet_SkinnedMesh` 以及视角锚点；一套 UV、两个材质、13 条 `UNITY_` Action。烘焙贴图已打包。
- `Tools/`：审计、烘焙、导出与贴图打包脚本。
- `migration_report.json`：源数据、排除项、缺失贴图和 Blender 动画对照结果。
- `unity_validation.json`：Unity 中实际绑定、蒙皮运动、时长、材质和贴图检查结果。

逐帧对照了全部 13 条动画，含最终相机变换共 34,968 个骨骼采样，最大位置误差约 `0.000000554 m`。Unity 中所有动画无未绑定路径，四个 punch 均产生有效顶点运动。另逐帧检查了所有动作的相机轨迹，最大旋转幅度误差约 `0.000014°`，位置误差小于 `0.000000023 m`。图集着色器支持本项目 PC 管线的 Forward+。

如需重新生成 Unity 资源，可运行 `Tools > Gauntlet > Rebuild Imported Assets`。它会重建本目录生成的预制体、控制器与预览场景；自定义玩法请放在其他脚本 / 场景中。

后续只修改动作或相机约束时，可复用现有图集后重新烘焙骨骼动作；模型 / UV / 材质发生变化时需要重新烘焙图集。`front_cap_patch.py` 会在每次导出副本上应用前缘封口，原作者工程保持原来的可编辑拓扑。
