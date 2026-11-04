# 共鸣者科学家：水晶侵蚀初版

打开 `Scientist_Resonator.blend`。已根据参考图，在原科学家模型上添加不对称的烟紫色水晶：左肩、上背为主，左前臂与右小腿为次级生长区域。保留了原科学家的面部、眼镜、衣物 UV、纹理与骨架；没有改变人物的基础姿态。工程按 1.80 米人物高度整理，预览灯光和晶体源形状分开存放。

源文件当前尚未保存的修改已保存在 `Source/Scientist_InputSnapshot.blend`，Sketchfab 原文件没有覆盖。`Preview` 内包含实际 Blender 渲染的正面、侧面、背面和斜前方预览。

## 调整几何节点

选中衣物对象 `sci_body_dirty.smd.001`，查看修改器 `AB | Crystal Infestation - Geometry Nodes`：

| 参数 | 作用 |
| --- | --- |
| Major Density | 主晶簇数量 |
| Micro Density | 细碎侵蚀晶体数量 |
| Crystal Length | 晶体整体长度 |
| Crystal Width | 晶体宽度 |
| Upward Growth | 向上生长倾向 |
| Seed | 生长分布与形状随机种子 |
| Show Crystals | 保留衣物并开关新增晶体 |

权重绘制可调整 `AB_Growth`（主生长范围）、`AB_Micro`（细碎晶体范围）和 `AB_Length`（局部尺寸）。小晶体不会沿全身平均铺开，脸部保持可辨识。晶体由三种棱柱源形状实例化，带不规则切面、细小崩边和根部至尖端的颜色变化；修改器未应用。手部网格也使用同一节点组，在腕部补充少量侵蚀。

## 材质

`AB | Aged fabric` 保留原贴图，叠加褪色、大块棕灰污斑、细粉尘、粗糙度变化和细微凹凸。袖口、衣摆和近地面位置有额外接触污渍。`ab_infection` 属性让晶体根部附近出现紫灰色矿化色变。`AB | Ashen skin` 降低肤色饱和度并增加尘灰。三个 `AB | Crystal` 材质控制烟紫主色、浅色切面与深色切面，保留折射、透射和反光参数。

## 文件与范围

- `01 | RESONATOR - Scientist`：角色与原骨架。
- `02 | Crystal Source Shapes`：隐藏的晶体形状源。
- `03 | Preview Studio`：灯光、地面、预览摄像机。
- `00 | Original Scene - archived helpers`：原文件中的辅助物体。
- `Tools/build_resonator.py`：从快照重建此版本的脚本。
- `Tools/validate_resonator.py`：检查修改器开关、密度、长度、种子、骨架和纹理。

这是 Blender 中可调整的侵蚀效果初版。衣服的破洞、撕裂和参考图中的垂头姿态没有制作。若用于 Unity，后续需要将程序材质烘焙成贴图，并为固化后的晶体补充蒙皮和检查动画；当前没有覆盖 Unity 的敌人或场景。几何节点放在骨架修改器之后，适合随表面预览；大幅动作时应检查表面随机散布是否发生变化。
