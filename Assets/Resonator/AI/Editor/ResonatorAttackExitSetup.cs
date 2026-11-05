using System;
using System.Linq;
using UnityEditor;
namespace Resonator.AI.Editor
{
    public static class ResonatorAttackExitSetup
    {
        public static void Connect(ResonatorTuning tuning)
        {
            string path=AssetDatabase.GetAssetPath(tuning);
            AssetDatabase.CopyAsset(path,AssetDatabase.GenerateUniqueAssetPath("Assets/Resonator/AI/Validation/Resonator_BeforeAttackExits.asset"));
            Undo.RecordObject(tuning,"Connect authored attack exits");
            var rules=(tuning.transitions??Array.Empty<ResonatorTransition>()).ToList();
            var scream=rules.FirstOrDefault(r=>r!=null&&r.from==EnemyState.Attack&&r.to==EnemyState.Screaming);
            if(scream!=null)scream.anySource=false;
            else rules.Add(new ResonatorTransition{label="Attack -> Screaming",from=EnemyState.Attack,to=EnemyState.Screaming,blend=tuning.scream.blend});
            var idle=rules.FirstOrDefault(r=>r!=null&&r.anySource&&r.to==EnemyState.CombatIdle);
            if(idle!=null)idle.label="Any -> Combat Idle";
            else
            {
                var specific=rules.FirstOrDefault(r=>r!=null&&r.from==EnemyState.Attack&&r.to==EnemyState.CombatIdle);
                rules.Add(new ResonatorTransition{label="Any -> Combat Idle",anySource=true,to=EnemyState.CombatIdle,
                    blend=specific!=null?specific.blend:tuning.idleRestless.blend,entryFrame=specific!=null?specific.entryFrame:-1,
                    exitFrame=specific!=null?specific.exitFrame:-1});
            }
            tuning.transitions=rules.ToArray();EditorUtility.SetDirty(tuning);AssetDatabase.SaveAssetIfDirty(tuning);
        }
    }
}
