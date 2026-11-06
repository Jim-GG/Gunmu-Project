namespace Resonator.AI
{
    // Compatibility component: existing prefabs and profiles retain their GUIDs.
    // Players and enemies execute the exact same pose mixer and frame-event clock.
    public sealed class ResonatorMotionPlayer : GameActions.ActionMotionPlayer { }
}
