using UnityEngine;

// Shared contract for terminals, pickups and doors.
public abstract class DemoInteractable : MonoBehaviour
{
    public abstract string Prompt { get; }
    public virtual bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled;
    public abstract void Interact(PlayerInteractor interactor);
}
