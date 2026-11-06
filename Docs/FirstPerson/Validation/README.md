# 共用动作管线与拳套验证

2026-11-05，Unity 6000.3.26f1。

| 报告 | 结果 | 验证重点 |
|---|---|---|
| GauntletActionsReport.json | 20 项通过 | 四档出拳、预输入及过期、双向取消、格挡能量、真实物理命中与吸附 |
| LiveAnimationReport.json | 6 项通过 | 真实 Update 下的姿态、旧 Controller 竞争、图重建、格挡及共鸣者伤害 |
| SharedWorkbenchReport.json | 4 项通过 | 四拳原始姿态、曲线绑定、第一人称盒体、实际混合权重 |
| EnemyRegressionReport.json | 20 项通过 | 原共鸣者行为、连击、格挡交互、死亡与导航 |
| AuthoringRegressionReport.json | 10 项通过 | 原命中盒、亚帧窗口、事件排序和预览隔离 |
| DisplacementRegressionReport.json | 10 项通过 | 原敌人位移预算、限速、碰撞和追击 |
| ExitRegressionReport.json | 8 项通过 | 原命中／打空收尾、过渡和优先级 |
| CrowdRegressionReport.json | 10 项通过 | 避障、绕过同伴、尸体通行与当前起手距离 |
| EnemyWorkbenchReport.json | 通过 | 原共鸣者盒体、混合、模型缩放和位移预览 |

手动逐帧驱动测试不能单独证明实际画面正确，所以增加了跨真实游戏帧的 LiveAnimation 验证。迁移时旧 Animator Controller 曾在原生动画更新阶段把手动 Playables 的出拳姿态覆盖为 Idle；现由共用播放器在接管期间挂起旧 Controller，释放时恢复。实际出拳骨骼与原 FBX 对应帧的角度误差为 0°。

最初的吸附预扫还曾因脚底贴地而把地板识别为零距离阻挡；现采用内缩预扫，并由完整 CharacterController 处理最终碰撞。四拳无命中提前停步时，测试测得总位移约 0.655 / 0.854 / 1.003 / 1.153 米（含 CharacterController 的碰撞修正）。正常命中后通常会更早停止。

共鸣者原有配置由用户调成头槌 1.3、踢击 1.67、挥拳 1.5 米，保留不重置。Crowd 回归中的头槌起手测试改为读取当前配置，不再假设固定为过去的 1.55 米测试距离。

预览截图见 Preview 目录；各脚本由项目自带 Pipeline 的 `script run` 执行。运行脚本需先进入对应模式；不把“尚未进入运行模式”的工具调用结果当成行为通过。
