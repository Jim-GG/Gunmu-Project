using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Resonator.AI.Editor
{
    public static class ResonatorHitboxDefaults
    {
        public static void Upgrade(ResonatorTuning tuning,bool backup=true)
        {
            if(!tuning||tuning.hitboxSchemaVersion>=2)return;
            if(backup)
            {
                string path=AssetDatabase.GetAssetPath(tuning);
                if(!string.IsNullOrEmpty(path))AssetDatabase.CopyAsset(path,AssetDatabase.GenerateUniqueAssetPath("Assets/Resonator/AI/Validation/Resonator_BeforeHitboxEditor.asset"));
            }
            Undo.RecordObject(tuning,"Upgrade to spatial hitboxes");
            // Migrate each untouched legacy beat independently, so tuning the kick
            // never prevents removal of phantom hits in an unrelated attack.
            if(tuning.hitboxSchemaVersion==0){Setup(tuning.headbutt,HitboxAnchor.Head);Setup(tuning.kick,HitboxAnchor.RightFoot);Setup(tuning.attack,HitboxAnchor.LeftHand);Setup(tuning.punch,HitboxAnchor.RightHand);}
            if(Matches(tuning.headbutt,37)){tuning.headbutt.hits[0].startFrame=36;tuning.headbutt.hits[0].endFrame=41;}
            if(Matches(tuning.kick,43)){tuning.kick.hits[0].startFrame=21;tuning.kick.hits[0].endFrame=27;}
            if(Matches(tuning.attack,43,82,115))
            {
                var first=tuning.attack.hits[0];first.startFrame=40;first.endFrame=46;
                tuning.attack.hits=new[]{first};
            }
            if(Matches(tuning.punch,37)){tuning.punch.hits[0].startFrame=36;tuning.punch.hits[0].endFrame=41;}
            foreach(var rule in tuning.transitions)
            {
                if(rule.to==EnemyState.Kicking&&Mathf.Approximately(rule.entryFrame,24))rule.entryFrame=12;
                if(rule.from==EnemyState.Kicking&&rule.to==EnemyState.Attack)
                {
                    if(Mathf.Approximately(rule.cancelFrameOnHit,49))rule.cancelFrameOnHit=29;
                    if(Mathf.Approximately(rule.cancelFrameOnMiss,58))rule.cancelFrameOnMiss=33;
                    if(Mathf.Approximately(rule.entryFrame,28))rule.entryFrame=20;
                }
            }
            tuning.hitboxSchemaVersion=2;EditorUtility.SetDirty(tuning);AssetDatabase.SaveAssetIfDirty(tuning);
        }
        static bool Matches(AttackBeat a,params float[] frames)=>a.hits!=null&&a.hits.Length==frames.Length&&a.hits.Select((h,i)=>Mathf.Approximately(h.startFrame,frames[i])&&Mathf.Approximately(h.endFrame,frames[i]+5)).All(v=>v);
        static HitWindow Sphere(string label,float from,float to,float damage,HitboxAnchor anchor,float radius)=>new HitWindow{label=label,startFrame=from,endFrame=to,damage=damage,anchor=anchor,shape=HitboxShape.Sphere,position=Vector3.zero,radius=radius,size=Vector3.one*radius*2};
        static void Setup(AttackBeat a,HitboxAnchor anchor)
        {if(a.hits==null)return;foreach(var h in a.hits){h.anchor=anchor;h.position=Vector3.zero;h.shape=HitboxShape.Sphere;h.radius=.3f;}}
    }
}
