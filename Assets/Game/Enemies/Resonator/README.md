# 共鸣者科学家：Blender → Unity

已移植当前雕刻版模型，桌面“共鸣者动作”内 11 个新动作全部完成 Mixamo → Valve 映射，保留原工程的 8 个动作，共 19 个 Unity 动作。

打开 `Scenes/ResonatorPreview.unity` 后点 Play，左侧按钮可切换动作，镜头跟随骨盆位移。游戏中使用 `Prefabs/ResonatorScientist.prefab`；该预制体不包含调试面板。`Resonator.controller` 提供 19 个可直接按名字播放的状态，当前使用 Generic 骨架。

模型在 `Models/Resonator.fbx`，动作在 `Animations`，URP 材质在 `Materials`。服装 4K、头部/皮肤 2K，分别使用颜色、切线法线、金属度/光滑度三张图。雕刻头部轮廓保留在低模几何中，高模雕刻法线与 AO 已合入烘焙贴图。水晶使用独立材质子网格，保留棱面、高光和环境反射；当前是 URP 不透明实时近似，透射/折射与 Blender Cycles 不会完全一致。

源工程另存到 `F:/3D Assets/Game/Enemies/Resonator_Scientist_Workshop/UnityTransfer_20261104/Resonator_Unity_Ready.blend`，保留原高模、雕刻烘焙源、原材质及导出副本。`Source/Input_Current.blend` 是工作开始时当前未保存修改的完整快照。

本轮修复过的关键问题：不能把所有小连通拓扑块当成晶体。服装、手脚恢复原有逐顶点多骨骼权重；只有真正的晶体子网格使用根部固定权重。映射按父子层级明确计算局部矩阵，并补偿 Mixamo T 姿态与 Valve A 姿态。输出预先三角化，避免 Unity 丢弃复杂多边形。

检查：19 个动作的骨骼曲线路径可绑定；Unity 播放模式逐一切换成功，蒙皮骨骼引用完整、权重归一化。与 Mixamo 原行走逐帧对照的上下臂、大小腿方向差在约 3.3° 内。`Validation/Final_*.png` 是修正后行走、攻击、踢腿和死亡关键姿势。

源动作的骨盆位移保留。尚未接敌人 AI、移动控制器、攻击伤害、布料模拟或碰撞避让；大幅动作的长衣摆和大型水晶可能发生局部交叠。
