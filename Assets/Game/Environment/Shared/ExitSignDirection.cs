using UnityEngine;

namespace FacilityProps
{
    /// <summary>Switches the complete baked side-arrow face, including the person's direction.</summary>
    [DisallowMultipleComponent]
    public sealed class ExitSignDirection : MonoBehaviour
    {
        public enum Direction { Right, Left }
        public Direction direction;
        public Material rightMaterial;
        public Material leftMaterial;
        [SerializeField] Renderer signRenderer;
        [SerializeField, Min(0)] int lensMaterialSlot;
        public void Configure(Renderer renderer,int slot)
        {signRenderer=renderer;lensMaterialSlot=slot;Apply();}
        public void SetDirection(Direction value){direction=value;Apply();}
        void OnEnable()=>Apply();
        void OnValidate()=>Apply();
        public void Apply()
        {
            if(!signRenderer)return;
            var material=direction==Direction.Left?leftMaterial:rightMaterial;
            var slots=signRenderer.sharedMaterials;
            if(!material||lensMaterialSlot>=slots.Length||slots[lensMaterialSlot]==material)return;
            slots[lensMaterialSlot]=material;signRenderer.sharedMaterials=slots;
        }
    }
}
