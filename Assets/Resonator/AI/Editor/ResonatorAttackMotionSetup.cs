using UnityEditor;
namespace Resonator.AI.Editor
{
    public static class ResonatorAttackMotionSetup
    {
        public static void ApplyPresets(ResonatorTuning tuning,bool backup=true)
        {
            if(backup)AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(tuning),AssetDatabase.GenerateUniqueAssetPath("Assets/Resonator/AI/Validation/Resonator_BeforeAttackDisplacement.asset"));
            Undo.RecordObject(tuning,"Configure attack pursuit movement");
            foreach(var state in new[]{EnemyState.Headbutt,EnemyState.Kicking,EnemyState.Attack,EnemyState.Punching})
            {
                var attack=tuning.AttackFor(state);
                if(attack.displacement!=null&&(attack.displacement.enabled||(attack.displacement.segments?.Length??0)>0))continue;
                attack.displacement=AttackDisplacement.Preset(state,attack);
            }
            EditorUtility.SetDirty(tuning);AssetDatabase.SaveAssetIfDirty(tuning);
        }
    }
}
