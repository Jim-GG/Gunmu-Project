using UnityEngine;

/// <summary>
/// 拳套拾取：靠近按 E 直接装备。
/// “前置终端”是可选依赖：只有显式指定了终端时，才要求先完成终端校准。
/// </summary>
public class GauntletPickup : DemoInteractable
{
    [SerializeField, InspectorName("前置终端（可留空）")] private CalibrationTerminal terminal;

    private bool taken;

    public override string Prompt
    {
        get
        {
            if (taken)
                return string.Empty;

            if (terminal != null && !terminal.IsSolved)
                return "支架已锁定，请先完成终端校准";

            return "[E] 拾取拳套";
        }
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return base.CanInteract(interactor) && !taken && !interactor.IsUIOpen;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (terminal != null && !terminal.IsSolved)
        {
            interactor.ShowFeedback("支架已锁定，请先完成校准。");
            return;
        }

        PlayerGauntletEquipment equipment = interactor.GetComponent<PlayerGauntletEquipment>();
        if (equipment == null)
        {
            Debug.LogError("Add PlayerGauntletEquipment to Player before using the pickup.", interactor);
            return;
        }

        if (!equipment.Equip())
            return;

        taken = true;
        interactor.ShowFeedback("已装备拳套！按住左键蓄力，松开后冲撞出拳。", 3f);
        gameObject.SetActive(false);
    }
}
