# 阿比森纳斯晶体模块包

保留原来的 3 根晶体，新增短粗、弯斜、刀片、断裂 4 种单体，共 7 种基础轮廓。另有 1 个宽断面平台核心，以及附着、斜生、爆发、平台 4 种组合晶簇，共 12 个 FBX。

- `Models`：12 个 FBX，生长根部为原点，导出副本已三角化并保留棱面法线。
- `Prefabs`：12 个装饰预制体，以及带交互碰撞体的 `CrystalAnchor_Wall`、`CrystalPlatform_Standable`。
- `Materials`：烟紫、浅色棱面、深色棱面 3 个共享材质，所有变种复用。
- `Shaders/CrystalFacet.shader`：棱面 PBR 高光、内部裂纹、轻微内辉和命中闪光；表面保持不透明，便于稳定排序与低成本复用。

可交互预制体通过 `CrystalSurface` 接入原拳套的实际 Hitbox 命中流程。装饰变种不默认启用牵引，避免每一根小碎晶都成为跑酷目标。站立平台使用简单 BoxCollider，以可靠落脚；尖端晶体不作为站立面。

Blender 工程位于 `ArtSource/Crystal/Crystal_ModularKit.blend`：`Crystal Kit` 场景的 `crystal` 集合包含全部变种；原始场景保留用户三根源模型及原材质节点。输入快照与载入成品前副本均在 `ArtSource/Crystal/Source`。
