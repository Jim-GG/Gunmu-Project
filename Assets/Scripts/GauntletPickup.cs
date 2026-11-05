using UnityEngine;

/// <summary>
/// 拳套拾取：靠近按 E 直接装备。
/// 装备后 <see cref="PlayerGauntletEquipment"/> 会启用 <see cref="PlayerChargedPunch"/>，
/// <see cref="PlayerGauntletView"/> 随之显示拳套外观。
/// </summary>
public class GauntletPickup : DemoInteractable
{
    private bool taken;

    public override string Prompt => taken ? string.Empty : "[E] 拾取拳套";

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return base.CanInteract(interactor) && !taken && !interactor.IsUIOpen;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        PlayerGauntletEquipment equipment = interactor.GetComponent<PlayerGauntletEquipment>();
        if (equipment == null)
        {
            Debug.LogError("使用拳套拾取前，请先给 Player 加上 PlayerGauntletEquipment。", interactor);
            return;
        }

        if (!equipment.Equip())
            return;

        taken = true;
        interactor.ShowFeedback("已装备拳套！按住左键蓄力，松开后冲撞出拳。", 3f);
        gameObject.SetActive(false);
    }
}
