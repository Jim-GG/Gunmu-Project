待审批：现代设施与苏联设施小道具

打开 FacilityProps_Review_v01.blend。Preview 中为 Blender 实际渲染；当前没有写入 Unity Assets。

两款箱子约 1 米见方，原点在底面中心，可堆放；把手、锁扣和包角为实体。现代箱使用白色涂层与深色护角；苏联箱使用旧绿漆、黄色包边和俄语货物编号。

两款出口灯箱约 0.96 米宽、0.28 米高、0.10 米厚，正面朝本地 -Y，原点在标牌中心。现代为 EXIT，苏联为 ВЫХОД。各有 Left / Right / Forward 三种箭头版本，字形和图标烘在 1024×256 的贴图里。

箱体主要材质沿用 Assets 中现有 ModernLab 和 SovietFacility 涂层纹理及 Inspector 参数；文字牌使用独立 1024×512 的印刷贴图。原有材质和 shader 没有改动。所有正式模型仅一个 UV0 通道，贴图均打包在 Blender 文件中。

Exports 中 8 个 FBX 为待导入文件。manifest.json 记录材质映射、尺寸、面数、发光和新贴图路径；validation.json 记录 Blender→FBX 回导验证。

审批后导入位置：现代资源进 Assets/Game/Environment/ModernLab，苏联资源进 Assets/Game/Environment/SovietFacility；复用现有材质，新增文字与出口发光材质，生成含碰撞的预制体。不会重新运行已有设施的重建工具。
