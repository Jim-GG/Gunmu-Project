# 共用动画工作台

本目录存放动画工作台及其相关 Inspector、过渡绘制器、拳套动作配置工具，均在 `Editor` 下，只参与编辑器编译。运行时动作系统位于 `Assets/Game/Shared/Actions`，敌人专用运行时位于 `Assets/Game/Enemies/Resonator/AI/Runtime`。

入口保持原样：`Tools → Game Actions → Animation Workbench 动画工作台`；原有的 `Tools → Resonator` 入口也保留。配置 Inspector 的打开按钮与工作台内“拳套 / 共鸣者”切换继续可用。

拳套配置：`Assets/Game/Shared/Actions/Resources/GauntletActions.asset`。
共鸣者配置：`Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset`。

本次仅调整文件位置与资源路径，不修改混合、取消、事件、Hitbox、位移、预输入或吸附的逻辑及参数。调整现有配置后保存即可；不必运行 Rebuild / Setup 工具。
