using UnityEngine;

public sealed class MomentumRemotePickup : DemoInteractable
{
    public override string Prompt => "[E] 拾取动量遥控器";

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor) || !interactor.TryGetComponent(out PlayerMovement player)) return;
        player.EquipMomentumRemote();
        interactor.ShowFeedback("已取得动量遥控器｜Q 减速，E 锁定或释放方块", 4f);
        gameObject.SetActive(false);
    }
}
