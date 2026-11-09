using UnityEngine;
namespace CrystalParkour
{
    // Font atlas rebakes must also reach the depth-tested world material.
    [ExecuteAlways,RequireComponent(typeof(TextMesh))]
    public sealed class WorldSpaceLabel:MonoBehaviour
    {
        MaterialPropertyBlock properties;
        void OnEnable(){Font.textureRebuilt+=Rebuilt;Refresh();}
        void OnDisable()=>Font.textureRebuilt-=Rebuilt;
        void Rebuilt(Font font){if(GetComponent<TextMesh>().font==font)Refresh();}
        void Refresh(){var text=GetComponent<TextMesh>();if(!text.font)return;var renderer=GetComponent<Renderer>();if(properties==null)properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);properties.SetTexture("_MainTex",text.font.material.mainTexture);renderer.SetPropertyBlock(properties);}
    }
}
