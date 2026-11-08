using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Resonator.AI;
namespace GameActions.Editor
{
    public static class GauntletActionsSetup
    {
        public const string ProfilePath="Assets/Game/Shared/Actions/Resources/GauntletActions.asset";
        [MenuItem("Tools/Game Actions/Configure Shared Gauntlet")]
        public static ActionSet Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
            var set=AssetDatabase.LoadAssetAtPath<ActionSet>(ProfilePath);
            if(!set)
            {
                System.IO.Directory.CreateDirectory("Assets/Game/Shared/Actions/Resources/Tracks");AssetDatabase.Refresh();
                set=ScriptableObject.CreateInstance<ActionSet>();set.firstPersonPreview=true;set.previewFieldOfView=65;
                set.previewModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Gauntlet/Prefabs/GauntletViewModel.prefab");
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Game/Player/Gauntlet/Animations/Gauntlet.controller");
                set.actions=controller.layers[0].stateMachine.states.Select(s=>
                {
                    var clip=s.state.motion as AnimationClip;var track=ScriptableObject.CreateInstance<ResonatorMotionTrack>();track.clip=clip;track.framesPerSecond=clip.frameRate;
                    string file=s.state.name.Replace(' ','_');AssetDatabase.CreateAsset(track,"Assets/Game/Shared/Actions/Resources/Tracks/"+file+".asset");
                    return new ActionDefinition{id=s.state.name,action=new AttackBeat{hits=Array.Empty<HitWindow>(),animation=new MotionBeat{track=track,speed=s.state.speed,blend=.07f,loop=clip.isLooping}}};
                }).ToArray();
                var damage=new[]{24f,40f,65f,160f};var starts=new[]{14f,2f,4f,6f};var ends=new[]{17f,5f,8f,11f};var pulls=new[]{.65f,.85f,1f,1.15f};
                var bridge=UnityEngine.Object.FindFirstObjectByType<ResonatorPlayerBridge>();
                for(int i=0;i<4;i++)
                {
                    var a=set.Find("punch"+(i+1));a.isAttack=true;a.chargeLevel=i;a.Motion.startFrame=i==0?10:0;
                    a.action.hits=new[]{new HitWindow{label="拳套有效帧",hitGroup="fist",startFrame=starts[i],endFrame=ends[i],damage=i==3?160:bridge?bridge.punchDamage[i]:damage[i],knockbackPerDamage=.012f,anchor=HitboxAnchor.Root,shape=HitboxShape.Box,position=new Vector3(0,-.25f,1.05f),size=new Vector3(.7f,.9f,.7f),minimumBlendWeight=.2f}};
                    a.action.displacement=new AttackDisplacement{enabled=true,stopOnHit=true,trackTarget=true,trackingStartFrame=starts[i],trackingEndFrame=ends[i],maxTrackingAngle=32,segments=new[]{new AttackMoveSegment{label="有效帧爆发吸附",startFrame=starts[i],endFrame=ends[i],maxDistance=pulls[i],maxSpeed=12,contactDistance=1.05f,progress=AnimationCurve.Linear(0,0,1,1)}}};
                    a.acquisitionRange=3.1f;a.acquisitionHalfAngle=32;
                }
                set.Find("charge").Motion.loop=false;set.Find("charge").Motion.speed=1;
                set.Find("charge full").Motion.endFrame=50;set.Find("charge full").Motion.loop=true;
                var rules=new System.Collections.Generic.List<ActionTransition>();
                foreach(var a in set.actions)rules.Add(new ActionTransition{label="Any → "+a.id,anySource=true,to=a.id,blend=a.id=="Parry-Hit"?.025f:a.id=="Parry-Holster"?.055f:a.id.StartsWith("punch")?.045f:.075f,cancelFrame=0});
                var cancels=new[]{18f,8f,11f,14f};
                for(int i=0;i<4;i++)foreach(var to in new[]{"charge","Parry-Start"})
                    rules.Insert(0,new ActionTransition{label="punch"+(i+1)+" → "+to,from="punch"+(i+1),to=to,cancelFrame=cancels[i],bufferStartFrame=Mathf.Max(set.Find("punch"+(i+1)).Motion.startFrame,cancels[i]-6),blend=to=="charge"?.075f:.055f});
                set.transitions=rules.ToArray();AssetDatabase.CreateAsset(set,ProfilePath);AssetDatabase.SaveAssetIfDirty(set);
            }
            // Only add references; never rebuild the arena or overwrite the user's tuning.
            const string path="Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{var c=root.GetComponent<FirstPerson.GauntletCombat>();if(c){c.profile=set;PrefabUtility.SaveAsPrefabAsset(root,path);}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            foreach(var c in UnityEngine.Object.FindObjectsByType<FirstPerson.GauntletCombat>(FindObjectsSortMode.None))
            {Undo.RecordObject(c,"Use shared gauntlet actions");c.profile=set;EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
            Selection.activeObject=set;return set;
        }
    }
}
