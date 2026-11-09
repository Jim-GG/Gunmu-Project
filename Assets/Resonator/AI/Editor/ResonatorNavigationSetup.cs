using UnityEditor;
namespace Resonator.AI.Editor
{
    public static class ResonatorNavigationSetup
    {
        public static void Configure(ResonatorTuning profile,bool backup=true)
        {
            if(backup)AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(profile),AssetDatabase.GenerateUniqueAssetPath("Assets/Game/Enemies/Resonator/AI/Validation/Resonator_BeforeCrowdNavigation.asset"));
            Undo.RecordObject(profile,"Configure crowd avoidance and attack ranges");
            if(profile.headbutt.startDistance<=0)profile.headbutt.startDistance=1.6f;
            if(profile.punch.startDistance<=0)profile.punch.startDistance=1.7f;
            if(profile.kick.startDistance<=0)profile.kick.startDistance=1.75f;
            if(profile.attack.startDistance<=0)profile.attack.startDistance=1.8f;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
}
