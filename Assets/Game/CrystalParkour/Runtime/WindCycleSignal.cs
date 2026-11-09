using UnityEngine;
namespace CrystalParkour
{
    public sealed class WindCycleSignal:MonoBehaviour
    {
        public WindVolume wind;public Renderer lamp;public TextMesh text;
        MaterialPropertyBlock block;
        void Update()
        {
            if(!wind)return;float amount=wind.Strength;
            if(text)text.text=amount>.05f?"AIR ON / JUMP":"AIR OFF / WAIT";
            if(lamp){if(block==null)block=new MaterialPropertyBlock();lamp.GetPropertyBlock(block);block.SetColor("_EmissionColor",Color.Lerp(new Color(1.5f,.15f,.02f),new Color(.1f,2.3f,.7f),amount));lamp.SetPropertyBlock(block);}
        }
    }
}
