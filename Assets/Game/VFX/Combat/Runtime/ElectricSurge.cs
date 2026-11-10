using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GameActions
{
    /// <summary>复用模型的短促电流覆层，不替换或修改原材质。</summary>
    public sealed class ElectricSurge : MonoBehaviour
    {
        readonly List<Renderer> overlays=new List<Renderer>();
        MaterialPropertyBlock block;float age,duration;bool ready;
        public bool Playing {get;private set;}
        public int OverlayCount=>overlays.Count;
        public void Play(Material material,float seconds)
        {
            if(!material)return;
            if(!ready)
            {
                block=new MaterialPropertyBlock();var sources=GetComponentsInChildren<Renderer>(true);
                foreach(var original in sources)
                {
                    if(!original.enabled||!original.gameObject.activeInHierarchy)continue;
                    Renderer overlay=null;Mesh mesh=null;
                    if(original is SkinnedMeshRenderer skin)
                    {
                        if(!skin.sharedMesh)continue;
                        var go=new GameObject("Transient electric surface");go.layer=original.gameObject.layer;go.transform.SetParent(original.transform,false);
                        var copy=go.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=skin.sharedMesh;copy.bones=skin.bones;copy.rootBone=skin.rootBone;copy.localBounds=skin.localBounds;copy.updateWhenOffscreen=skin.updateWhenOffscreen;overlay=copy;mesh=skin.sharedMesh;
                    }
                    else if(original is MeshRenderer && original.TryGetComponent<MeshFilter>(out var filter)&&filter.sharedMesh)
                    {
                        var go=new GameObject("Transient electric surface");go.layer=original.gameObject.layer;go.transform.SetParent(original.transform,false);go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;overlay=go.AddComponent<MeshRenderer>();mesh=filter.sharedMesh;
                    }
                    if(!overlay)continue;
                    var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=material;overlay.sharedMaterials=materials;
                    overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlays.Add(overlay);
                }
                ready=true;
            }
            age=0;duration=Mathf.Max(.05f,seconds);Playing=true;Apply(1);
        }
        void Apply(float value){foreach(var r in overlays)if(r){r.enabled=value>0;block.SetFloat("_Envelope",value);r.SetPropertyBlock(block);}}
        public void Tick(float dt){if(!Playing)return;age+=Mathf.Max(0,dt);float t=Mathf.Clamp01(age/duration);Apply((1-t)*(1-t));if(t>=1)Playing=false;}
        void Update(){if(!GameplayGate.IsBlocked)Tick(Time.deltaTime);}
        public void Stop(){Playing=false;if(block!=null)Apply(0);}
        void OnDisable()=>Stop();
    }
}
