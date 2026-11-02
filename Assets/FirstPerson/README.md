# 第一人称与拳套

运行 `Tools > Gauntlet > Enable First Person Gameplay` 配置拳套预览场景。可复用角色为 `Prefabs/FirstPersonPlayer.prefab`。仅补充测试敌人可运行 `Tools > Gauntlet > Configure Parry Training`。

WASD 移动，Shift 跑步，Space 跳跃，鼠标观察，Esc 释放鼠标，点击重新锁定。左键短按（默认不超过 0.12 秒）立即 punch1。右键按住格挡，R 恢复训练血量。

长按左键播放 charge（30 FPS）。非短按在 0–12 帧释放会排队到第 13 帧，然后 punch2；13–61 帧释放 punch2；62–91 帧释放 punch3；92 帧起释放 punch4。32 / 62 / 92 帧通知三个蓄力档位。到第 92 帧仍持有则循环 Blender `charge full` 的 0–50 帧，直到释放。62 和 92 按进入下一档处理；阈值可在 `GauntletCombat.policy` 调整。

右键按下立即 Parry-Start，按住进入 Parry-Keep 循环。进入、持续和受击状态均可以格挡；成功挡住来自正面 120 度内的一次攻击，会立即 Parry-Hit，结束后继续 Parry-Keep。松开右键立即 Parry-Holster，结束回 Idle，收起期间不再格挡。`blockAngle` 可调整防御角度。右键可以取消蓄力；出拳时按住右键会排队，出拳结束进入格挡。格挡时忽略左键。

成功格挡一次积攒一级能量，最多三级；能量用完前保留，重复受击仍有动画反馈。蓄力进入下列区间时自动消耗一级，动画、材质蓄力值和摄像机骨骼同步跳到目标帧。跳帧保留每段末尾的档位反馈，每段每次蓄力只可消费一次。

| 跳过区间（含首尾） | 目标帧 | 消耗 |
| --- | --- | --- |
| 13–30 | 31 | 1 级 |
| 33–60 | 61 | 1 级 |
| 63–90 | 91 | 1 级 |

一级能量只跳第一段；二级依次跳前两段；三级跳三段，再由 91 正常推进至 92 进入 charge full。无能量时正常播放。0 / 1 / 2 / 3 级能量从按下到满蓄力约为 3.07 / 2.47 / 1.53 / 0.60 秒。短按 punch1 以及在 0–12 帧松开后排队的 punch2 均不消耗能量，未用能量可留给下次蓄力。快速点击按真实按住时间判断，不受动画跳帧影响。

预览场景中正前方的胶囊测试敌人会在 3.2 米内反复抬手预警（0.9 秒）并攻击，随后恢复 1.2 秒。身体预警时变黄，成功格挡变青，未挡住变红并扣 10 血；离开范围可躲开攻击，障碍物可阻挡攻击。血量归零后敌人暂停，按 R 恢复。界面显示当前状态、能量、蓄力帧数和攻击结果。测试敌人是验证格挡用的占位物；正式敌人和拳击伤害可通过下列接口接入。

模块：`FirstPersonInput` 读取输入；`FirstPersonMotor` 负责 CharacterController、重力和观察；`GauntletCombat` 管理战斗状态和能量；`FirstPersonVitals` 接收伤害并发送 `Damaged/Died`；`ParryTrainingEnemy` 和 `GauntletTrainingHUD` 是可移除的训练组件。训练 UI 独立于角色预制体。

接入敌人：在一次攻击命中的时刻调用 `combat.TryBlock(attackerPosition)`；返回 true 即已处理格挡和加能，返回 false 再调用 `vitals.ReceiveDamage(damage)`。每次攻击只提交一次伤害。输入接口为 `Press/Release/BeginBlock/EndBlock/Cancel`。事件 `TierChanged`、`AttackStarted`、`AttackFinished`、`AttackBlocked`、`EnergyChanged` 和 `ChargeSkipped` 可接音效、正式伤害系统、特效及 UI。`ChargeSkipped` 参数是当前更新跳过的段数，低帧率可一次跨多段。

攻击期间忽略新的左键；失焦 / Esc 取消蓄力和格挡，保留未消费的能量。层级为 Player → View Pivot → 输出 Camera、拳套骨架、Cinemachine Camera，三者同级；鼠标修改 Pivot，Cinemachine 复制 camera_target 的动作偏移。

验证脚本位于 `ArtSource/Gauntlet/Validation/ParryValidation.cs`，包含动画循环/绑定、不同帧率跳帧一致性、四种初始能量、受击打断与松开、能量上限、短按/提前释放保留能量和真实敌人攻击验证；原有 `GameplayTest.cs` 验证四档出拳和移动观察。
