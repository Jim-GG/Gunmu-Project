# 预览相机约束

当前预览使用 Cinemachine 3.1.7。`Gauntlet Preview Camera` 上的 `Cinemachine Brain` 接收 `Gauntlet Animation Camera` 的输出。

虚拟相机的 Rotation Control 是 `Rotate With Follow Target`，Damping 为 0。Tracking Target 指向 `camera_target/Camera Rotation Offset`；这个子节点保留 Idle 相机朝向的校准偏移。

拳套动画骨架与输出 Camera 同级，避免相机与其子骨骼互相驱动。旧 `GauntletCameraMotion` 在该场景中停用。Animator 播放或编辑器预览改变 `camera_target` 时，Cinemachine 会同步更新输出相机的旋转。

连接命令：`Tools > Gauntlet > Connect Preview Camera with Cinemachine`。创建预览场景和挂到选中相机的菜单也会自动建立相同连接。操作支持 Undo，连接前的场景副本保存在 `Temp/GauntletCameraFix/BeforeCameraConstraint.unity`。
