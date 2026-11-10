using UnityEngine;
namespace GameActions
{
    [RequireComponent(typeof(ScreenEffectsController))]
    public sealed class RadialBlurController : MonoBehaviour
    {
        ScreenEffectsController Controller => GetComponent<ScreenEffectsController>();
        public void PlayAtWorldPosition(Vector3 worldPosition, float strength = .1f) => Controller.PlayRadial(Controller.Project(worldPosition), strength);
        public void PlayAtViewportPosition(Vector2 viewportPosition, float strength = .1f) => Controller.PlayRadial(viewportPosition, strength);
        public void PlayAtScreenCenter(float strength = .1f) => Controller.PlayRadial(new Vector2(.5f, .5f), strength);
        public void Stop() => Controller.Stop();
        [ContextMenu("预览径向模糊")] void Preview() => PlayAtScreenCenter();
    }
}
