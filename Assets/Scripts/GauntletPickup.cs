using UnityEngine;

public class GauntletPickup : DemoInteractable
{
    [SerializeField] private CalibrationTerminal terminal;
    private bool taken;
    public override string Prompt => terminal != null && terminal.IsSolved
        ? "[E] 拾取拳套" : "支架已锁定，请先完成终端校准";

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return base.CanInteract(interactor) && !taken && !interactor.IsUIOpen;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (terminal == null)
        {
            Debug.LogError("GauntletPickup: assign Terminal in Inspector.", this);
            return;
        }
        if (!terminal.IsSolved)
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
        if (!equipment.Equip()) return;
        taken = true;
        interactor.ShowFeedback("已装备拳套！按住左键蓄力，松开后冲撞出拳。", 3f);
        gameObject.SetActive(false);
    }
}
