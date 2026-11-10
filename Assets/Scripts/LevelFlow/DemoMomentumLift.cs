using UnityEngine;

public sealed class DemoMomentumLift : MonoBehaviour, IPuzzleTriggerable, ILevelExitGate
{
    [SerializeField, InspectorName("弹射方块")] private KineticBox box;
    [SerializeField, InspectorName("玩家碰撞体")] private CharacterController player;
    [SerializeField, Min(0.1f), InspectorName("向上弹射速度")] private float launchSpeed = 5.6f;

    public bool IsSatisfied { get; private set; }
    public bool CanTrigger => box != null && player != null
        && player.TryGetComponent(out PlayerMovement movement) && movement.HasMomentumRemote;

    public void Trigger()
    {
        if (!CanTrigger) return;
        box.ResetBox();
        Physics.SyncTransforms();
        box.ApplyFreeImpulse(Vector3.up, launchSpeed);
    }

    private void LateUpdate()
    {
        if (IsSatisfied || player == null || box == null || !box.IsMomentumLocked
            || box.Collider == null || !player.TryGetComponent(out PlayerMovement movement)
            || movement.CurrentVelocity.y > .25f) return;

        Bounds body = player.bounds;
        Vector3 foot = new Vector3(body.center.x, body.min.y, body.center.z);
        Ray down = new Ray(foot + Vector3.up * .08f, Vector3.down);
        if (box.Collider.Raycast(down, out RaycastHit surface, .08f + player.skinWidth + .08f)
            && surface.normal.y > .7f)
            IsSatisfied = true;
    }
}
