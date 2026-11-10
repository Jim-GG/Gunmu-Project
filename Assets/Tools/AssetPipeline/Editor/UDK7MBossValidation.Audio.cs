using System.Linq;
using UnityEngine;
using UDK7M;
using GameActions;
namespace AssetPipeline.Editor
{
    public static partial class UDK7MBossValidation
    {
        static void CheckAudioAndRails()
        {
            foreach(var pillar in arena.pillars)
            {
                var rails=pillar.transform.Find("Upper platform guardrails");Assert(rails&&rails.childCount==8,"Upper platform is missing rail segments");
                foreach(var c in rails.GetComponentsInChildren<BoxCollider>())Assert(c.bounds.min.y>8.9f&&c.bounds.min.y<9.05f&&c.bounds.max.y>9.7f,"Rail floats above platform or is too low");
                foreach(float side in new[]{-1f,1f})foreach(var hit in Physics.RaycastAll(pillar.transform.TransformPoint(new Vector3(0,9.6f,side*2.6f)),Vector3.forward*side,1.2f,~0,QueryTriggerInteraction.Ignore))Assert(!hit.transform.IsChildOf(rails),"Guardrail blocked an exit gate");
            }
            Pass("Four upper platforms have fitted guardrails and two clear exit gates each");
            Assert(!arena.feedback.audio&&!arena.GetComponent<UDK7MAudio>(),"Old global audio manager still active");
            var loops=arena.boss.GetComponentsInChildren<MachineLoopAudio>(true);
            var engine=loops.Single(l=>!l.drill);var drill=loops.Single(l=>l.drill);
            Assert(engine.source.clip&&engine.source.clip.name=="机器轰鸣"&&engine.source.loop&&engine.source.isPlaying,"Diesel engine loop missing or not playing");
            Assert(drill.source.clip&&drill.source.clip.name=="电钻循环"&&drill.transform.IsChildOf(arena.boss.drill),"Drill loop not attached to drill bone");
            Assert(!arena.boss.GetComponentsInChildren<PlayerObjectAudio>(true).Any()&&!arena.boss.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Footstep AudioSource"),"Tracked boss has footsteps");
            Assert(engine.source.spatialBlend>=.9f&&drill.source.spatialBlend>=.8f,"Continuous machinery is not spatial enough");
            var prepare=arena.boss.GetComponentsInChildren<MachinePrepareAudio>(true).Single();
            Assert(prepare.source.clip&&prepare.source.clip.name=="metal_scrape_loop"&&prepare.source.loop&&prepare.source.spatialBlend<.67f,"Missing looping prepare warning");
            arena.boss.ForceState(BossState.SlamPrepare);prepare.Sync();Assert(prepare.source.isPlaying,"Slam prepare warning failed to start");
            arena.boss.ForceState(BossState.ChargePrepare);prepare.Sync();Assert(prepare.source.isPlaying,"Charge does not share prepare loop");
            prepare.source.time=.4f;arena.boss.ForceState(BossState.ChargeHold);prepare.Sync();Assert(prepare.source.isPlaying&&prepare.source.time>=.39f,"Prepare loop restarted/stopped on hold transition");arena.boss.ForceState(BossState.ChargeRecover);prepare.Sync();Assert(!prepare.source.isPlaying,"Warning did not stop in recover");
            Pass("Tracked boss has no footsteps; Slam/Charge share looping preparation warning with automatic stop");
            foreach(var binding in arena.boss.GetComponentsInChildren<ObjectAudio>(true))Assert(binding.sounds.All(s=>s.clips!=null&&s.clips.Any(c=>c)),"Missing assigned local clip on "+binding.name);
            arena.boss.ForceState(BossState.SlamHold);arena.boss.Tick(.12f);drill.SendMessage("Update");Assert(drill.source.isPlaying==arena.boss.DrillSpinning,"Drill loop does not follow rotation");
            arena.boss.ForceState(BossState.DrillCapture);drill.SendMessage("Update");Assert(!drill.source.isPlaying,"Drill sound continues during capture pause");arena.boss.ForceState(BossState.Idle);
            Pass("Engine and drill own independent 3D loop sources; global audio manager removed");
            var drillEvents=arena.boss.drill.GetComponent<ObjectAudio>();Assert(drillEvents&&drillEvents.source,"Drill event source missing from prefab");
            arena.feedback.Sound(UDK7MSound.SlamLight,arena.boss.DrillTip,arena.boss.drill);Assert(drillEvents.LastClip&&drillEvents.LastClip.name=="BigMetalImpact","Light slam uses wrong sound");
            arena.feedback.Sound(UDK7MSound.TripleFinalImpact,arena.boss.DrillTip,arena.boss.drill);Assert(drillEvents.LastClip.name=="BigMetalImpact4","Final slam uses wrong sound");
            Assert(drillEvents.source.spatialBlend<=.15f&&drillEvents.source.minDistance>=24&&drillEvents.source.maxDistance>=140&&drillEvents.source.rolloffMode==AudioRolloffMode.Linear,"Slam cannot be heard across arena");
            var local=drillEvents.source.transform.localPosition;var prior=arena.boss.transform.position;arena.boss.transform.position+=Vector3.right*3;
            Assert(drillEvents.source.transform.IsChildOf(arena.boss.drill)&&drillEvents.source.transform.localPosition==local,"Event sound detached from owner");arena.boss.transform.position=prior;
            Pass("TripleSlam light/final mappings and moving drill sound ownership");
            var bossEvents=arena.boss.GetComponent<ObjectAudio>();var playerEvents=arena.player.GetComponent<ObjectAudio>();
            foreach(var pair in new[]{(bossEvents,UDK7MSound.OverloadCharge),(drillEvents,UDK7MSound.ResonanceBurst),(playerEvents,UDK7MSound.GroundPoundImpact)})
            {
                var cue=pair.Item1.sounds.Single(s=>s.cue==pair.Item2);Assert(cue.spatialBlend==0&&cue.clips.Single().name=="大共振音效","Major resonance not mapped to pure 2D recording");
                float volume=pair.Item1.source.volume;try{pair.Item1.source.volume=0;pair.Item1.Play(pair.Item2,pair.Item1.transform.position);Assert(pair.Item1.source.spatialBlend==0&&pair.Item1.LastClip.name=="大共振音效","Major resonance playback routing incorrect");}finally{pair.Item1.source.Stop();pair.Item1.source.volume=volume;}
            }
            Assert(arena.warningSounds.Length==0,"Five simultaneous falling-rock warning sounds still mask heavy slam");
            Assert(!bossEvents.sounds.Any(s=>s.cue==UDK7MSound.ChargeStart||s.cue==UDK7MSound.CycleRestart),"Unnecessary charge/restart embellishments remain");
            var landing=arena.localDebrisPrefab.GetComponent<ObjectAudio>().sounds.Single(s=>s.cue==UDK7MSound.CrystalLanding);Assert(landing.clips.Single().name=="BigImpact","Falling crystal still uses glass landing sound");
            var chargeAudio=arena.player.GetComponent<GauntletChargeAudio>();Assert(chargeAudio&&chargeAudio.stages.clip.name=="三段蓄力"&&chargeAudio.fullLoop.clip.name=="蓄力满循环音效","Provided gauntlet recordings not assigned");
            var combat=arena.player.gauntlet;bool enabled=combat.enabled;float stageVolume=chargeAudio.stages.volume,fullVolume=chargeAudio.fullLoop.volume;
            try
            {
                chargeAudio.stages.volume=chargeAudio.fullLoop.volume=0;combat.enabled=true;combat.ResetResonance();combat.Cancel();combat.Press();combat.Tick(.5f);chargeAudio.Sync();
                Assert(chargeAudio.stages.isPlaying&&!chargeAudio.fullLoop.isPlaying,"Three-stage recording not playing during charge");
                combat.Cancel();combat.AddEnergy(3);combat.Press();combat.Tick(.45f);chargeAudio.Sync();
                float expected=combat.ChargeSeconds/(combat.policy.full/combat.policy.framesPerSecond)*chargeAudio.stages.clip.length;
                Assert(Mathf.Abs(chargeAudio.stages.time-expected)<.1f,"Charge recording did not seek with energy skip");
                combat.Tick(.7f);chargeAudio.Sync();Assert(combat.State==FirstPerson.GauntletCombat.Phase.Full&&chargeAudio.fullLoop.isPlaying&&chargeAudio.fullLoop.loop&&!chargeAudio.stages.isPlaying,"Full charge did not switch to looping recording");
                combat.Release();chargeAudio.Sync();Assert(!chargeAudio.stages.isPlaying&&!chargeAudio.fullLoop.isPlaying,"Charge audio did not stop on release");
            }
            finally{combat.Cancel();combat.ResetResonance();combat.enabled=enabled;chargeAudio.Stop();chargeAudio.stages.volume=stageVolume;chargeAudio.fullLoop.volume=fullVolume;}
            Pass("Three major resonances play pure 2D; arena-wide slams, BigImpact crystal landing and provided charge/skip/full-loop recordings");
            var library=Resources.Load<SurfaceSoundLibrary>("SurfaceSounds");Assert(library,"Surface sound presets missing");
            var metal=library.surfaces.Single(s=>s.surface==SoundSurface.Metal);Assert(metal.contacts.Length==4&&metal.fist&&metal.fist.name=="BigMetalImpact3","Metal/fist sound mapping incomplete");
            foreach(var name in new[]{"metal_clang5","metalgrate_land1","metalgrate_land2","metalgrate_land3"})Assert(metal.contacts.Any(c=>c.name==name),"Missing metal contact "+name);
            var crystals=library.surfaces.Single(s=>s.surface==SoundSurface.Crystal);Assert(crystals.contacts.Any(c=>c.name=="glass_break1")&&crystals.contacts.Any(c=>c.name=="glass_break2")&&crystals.contacts.Any(c=>c.name.StartsWith("Crystal")),"Crystal contact mapping incomplete");
            foreach(var surface in new[]{SoundSurface.Concrete,SoundSurface.Wood,SoundSurface.Metal}){var p=library.surfaces.Single(s=>s.surface==surface);Assert(p.contacts.Length>0&&p.steps.Length>0,"Missing contact/footsteps for "+surface);}
            Assert(!arena.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Any(t=>t.name=="Contact AudioSource"),"Old per-surface contact sources remain in scene");
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);temp.name="Temporary metal material";temp.transform.position=new Vector3(0,100,0);
            var owner=new GameObject("Temporary hitbox sound owner");owner.transform.position=temp.transform.position;
            float frame=arena.boss.runner.motion.SourceFrame;
            try
            {
                var tag=temp.AddComponent<SurfaceAudio>();tag.surface=SoundSurface.Metal;tag.library=library;var collider=temp.GetComponent<Collider>();
                var audio=owner.AddComponent<SurfaceContactAudio>();
                Assert(audio.Contact(collider,temp.transform.position,true)&&audio.LastClip.name=="BigMetalImpact3","Fist metal contact did not choose BigMetalImpact3");
                Assert(!audio.Contact(collider,temp.transform.position,false),"Contact cooldown failed");Assert(audio.sources.All(v=>v.transform.IsChildOf(owner.transform)),"Contact source does not belong to attacker");
                var source=audio.sources[0];source.maxDistance=9;owner.SetActive(false);Assert(!source.isPlaying,"Recycled attacker leaked contact audio");owner.SetActive(true);Assert(audio.Contact(collider,temp.transform.position,false),"Pool reuse retained cooldown");Assert(source.maxDistance==9,"Playback overwrote Inspector source settings");
                audio.interval=0;audio.enabled=false;audio.enabled=true;Physics.SyncTransforms();
                var h=new Resonator.AI.HitWindow{enabled=true,anchor=Resonator.AI.HitboxAnchor.Root,shape=Resonator.AI.HitboxShape.Sphere,position=Vector3.zero,radius=1,startFrame=0,endFrame=10000,minimumBlendWeight=0,contactEffects=false,repeat=HitRepeat.Once};
                var action=new ActionDefinition{isAttack=true};action.action.hits=new[]{h};var contacts=new ActionContact();var boxes=new ActionHitbox(owner.transform);var motion=arena.boss.runner.motion;int before=audio.ContactsPlayed;
                for(int i=0;i<3;i++){motion.Seek(motion.Beat.startFrame+i*2);contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.2f);}
                Assert(audio.ContactsPlayed==before+1,"Single attack repeated its sound across samples");
                contacts.Reset();h.repeat=HitRepeat.EffectsOnly;for(int i=0;i<3;i++){motion.Seek(motion.Beat.startFrame+i*2);contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.2f);}
                Assert(audio.ContactsPlayed==before+4,"Sustained contact lost repeat audio");
                Assert(audio.sources.Length==4&&temp.GetComponentsInChildren<AudioSource>().Length==0,"Contact sampling populated the environment with sources");
                Pass("Hitbox sounds use four attacker-owned voices; single-hit deduplication, repeats and pool reset");
                var feet=new GameObject("Temporary footstep actor");feet.transform.position=temp.transform.position+Vector3.up*.51f;
                try
                {
                    var steps=feet.AddComponent<PlayerObjectAudio>();steps.stepDistance=1;steps.EnsureSource();var original=steps.stepSource;
                    steps.TickStride(Vector3.right*.4f,true,.1f);Assert(steps.FootstepsPlayed==0,"Footstep played before a full step");steps.TickStride(Vector3.right*.6f,true,.1f);
                    Assert(steps.FootstepsPlayed==1&&steps.LastGroundSurface==SoundSurface.Metal,"Foot plant did not select metal below actor");
                    steps.TickStride(Vector3.zero,true,.1f);steps.TickStride(Vector3.right,false,.1f);Assert(steps.FootstepsPlayed==1,"Idle or airborne movement emitted footsteps");
                    steps.Footstep();Assert(steps.FootstepsPlayed==2&&steps.stepSource==original&&steps.stepSource.transform.IsChildOf(feet.transform),"Footsteps did not reuse the actor-owned source");
                    Assert(Mathf.Abs(steps.stepSource.transform.position.y-(temp.transform.position.y+.54f))<.01f,"Footstep sound not positioned at ground contact");
                    Assert(temp.GetComponentsInChildren<AudioSource>().Length==0,"Footstep material lookup created ground sources");
                    Pass("Stride/airborne/idle footstep timing, ground material lookup and reusable source beneath actor");
                }finally{UnityEngine.Object.DestroyImmediate(feet);}
            }finally{arena.boss.runner.motion.Seek(frame);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(temp);}
            Assert(arena.localDebrisPrefab&&arena.localDebrisPrefab.GetComponent<ObjectAudio>().sounds.Any(s=>s.cue==UDK7MSound.RockLanding&&s.clips.Any(c=>c.name=="BigImpact")),"Falling debris has no local BigImpact source");
            Assert(arena.player.GetComponent<PlayerObjectAudio>()&&arena.player.GetComponent<ObjectAudio>(),"Player local footsteps/events missing");
            Assert(arena.tuning.decisionInterval.x<=.3f&&arena.tuning.decisionInterval.y<=.55f,"Aggressive decision timing not applied");
            Pass("Material contacts, fist override, contact rate limit, pool reset, footsteps and falling-object sounds configured");
        }
    }
}




