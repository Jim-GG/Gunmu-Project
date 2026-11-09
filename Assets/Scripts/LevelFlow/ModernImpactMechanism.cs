using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ModernImpactMechanism : MonoBehaviour, ILevelExitGate
{
    [Min(0f)] public float minimumImpactSpeed = 2f;
    public KineticBox requiredBox;
    public bool requireBankShot;
    public GameObject removeOnImpact, revealOnImpact;
    public Transform counterweight, landing, liftedDoor, doorOpenPosition;
    [Min(.1f)] public float fallSpeed = 7f;
    private bool reflected, triggered;
    public bool IsSatisfied { get; private set; }

    public void MarkReflection() => reflected = true;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || GameplayGate.IsBlocked || (requireBankShot && !reflected)) return;
        var box = other.GetComponentInParent<KineticBox>();
        if (!box || (requiredBox && requiredBox != box) || box.IsMomentumLocked
            || box.PreImpactVelocity.magnitude < minimumImpactSpeed) return;
        triggered = true;
        if (removeOnImpact) removeOnImpact.SetActive(false);
        if (!counterweight || !landing) Finish();
    }

    private void Update()
    {
        if (!triggered || IsSatisfied || !counterweight || !landing) return;
        counterweight.position = Vector3.MoveTowards(counterweight.position, landing.position, fallSpeed * Time.deltaTime);
        if (counterweight.position == landing.position) Finish();
    }

    private void Finish()
    {
        IsSatisfied = true;
        if (revealOnImpact) revealOnImpact.SetActive(true);
        if (liftedDoor && doorOpenPosition) liftedDoor.position = doorOpenPosition.position;
    }
}
