from pathlib import Path
p=Path('Assets/Game/Shared/Actions/Runtime/GauntletChargeAudio.cs');s=p.read_text(encoding='utf-8-sig').replace('GauntletCombat combat;','GauntletPresentationSource state;');start=s.index('            if(!combat)');end=s.index('\n        public void Stop()',start);s=s[:start]+'''            if (!state) state = GauntletPresentationSource.Ensure(gameObject);
            EnsureSources();
            if (!state.IsUsable) { Stop(); return; }
            if (GameplayGate.IsBlocked) { stages.Pause(); fullLoop.Pause(); return; }
            if (state.IsFull)
            {
                if (stages.isPlaying) stages.Stop();
                fullLoop.UnPause();
                if (fullLoop.clip && !fullLoop.isPlaying) fullLoop.Play();
                return;
            }
            if (fullLoop.isPlaying) fullLoop.Stop();
            if (!state.IsCharging || !stages.clip) { Stop(); return; }
            float duration = Mathf.Max(.01f, state.FullDuration);
            float time = Mathf.Clamp(state.Seconds / duration * stages.clip.length, 0, Mathf.Max(0, stages.clip.length - .005f));
            stages.pitch = Mathf.Clamp(stages.clip.length / duration * state.PlaybackSpeed, .1f, 3);
            if (state.AtPartialCap) { stages.Pause(); stages.time = time; return; }
            stages.UnPause();
            if (!stages.isPlaying) { stages.Play(); stages.time = time; }
            else if (Mathf.Abs(stages.time - time) > seekTolerance) stages.time = time;
        }
''' +s[end:];p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/VFX/Combat/Runtime/WeaponEnergyVFXController.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('SkinnedMeshRenderer[] overlays;', 'GauntletPresentationSource state;\n  SkinnedMeshRenderer[] overlays;');needle='  void Create()';s=s.replace(needle,'''  public static WeaponEnergyVFXController Ensure(PlayerChargedPunch c){var v=c.GetComponent<WeaponEnergyVFXController>();if(!v)v=c.gameObject.AddComponent<WeaponEnergyVFXController>();v.state=GauntletPresentationSource.Ensure(c.gameObject);if(!v.library)v.library=AdvancedCombatVFXLibrary.Load();return v;}
  public int OverlayCount=>overlays==null?0:overlays.Length;
  public float DisplayedCharge=>charge;
''' + needle);s=s.replace('if(overlays!=null||!library||!library.energyMaterial||!library.gauntletOverlay||!combat||!combat.animationPlayer)return;var source=combat.animationPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true);','if(!state)state=GauntletPresentationSource.Ensure(gameObject);if(overlays!=null||!library||!library.energyMaterial||!library.gauntletOverlay||!state.Animation)return;var source=state.Animation.GetComponentsInChildren<SkinnedMeshRenderer>(true);');s=s.replace('overlays=list.ToArray();block=new MaterialPropertyBlock();','if(list.Count>0){overlays=list.ToArray();block=new MaterialPropertyBlock();}');s=s.replace('if(!combat)combat=GetComponent<GauntletCombat>();Create();','if(!state)state=GauntletPresentationSource.Ensure(gameObject);Create();');a='bool charging=combat&&(combat.State==GauntletCombat.Phase.Charging||combat.State==GauntletCombat.Phase.ReleasePending||combat.State==GauntletCombat.Phase.Full);';assert a in s;s=s.replace(a,'bool charging=state.IsCharging&&!GameplayGate.IsBlocked;');s=s.replace('combat.ChargeSeconds/Mathf.Max(.1f,combat.policy.full/combat.policy.framesPerSecond)','state.Seconds/Mathf.Max(.1f,state.FullDuration)');s=s.replace('releaseAge+=Time.deltaTime;','releaseAge+=state.UsesPuzzleRules?Time.unscaledDeltaTime:Time.deltaTime;');s=s.replace('if(preview)charge=previewCharge;','if(GameplayGate.IsBlocked)return;\n   if(preview)charge=previewCharge;');a=s.index('var source=combat.animationPlayer.meshRenderer;');b=s.index('if(!flashAnchor)return;',a);s=s[:a]+'var animation=state.Animation;if(!animation||!animation.meshRenderer)return;var source=animation.meshRenderer;if(!flashAnchor)flashAnchor=state.WeaponAnchor;'+s[b:];s=s.replace('if(!r.enabled)continue;','if(!r.enabled)continue;');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Player/PlayerGauntletView.cs');s=p.read_text(encoding='utf-8-sig').replace('    public bool IsVisible =>','    public GauntletAnimationPlayer AnimationPlayer => animationPlayer;\n\n    public bool IsVisible =>');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Player/PlayerChargedPunch.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        if (aim == null) aim = transform;','        if (aim == null) aim = transform;\n        GameActions.LevelPlayerPresentation.Ensure(gameObject);');s=s.replace('            ChargedPunchPulse.Spawn(aim.position + aim.forward * reach, -aim.forward, attackTier);\n','');s=s.replace('receiver != null && receiver.IsFlesh);','receiver != null && receiver.IsFlesh, collider);');s=s.replace('        ChargedPunchPulse.Spawn(point + normal * 0.03f, normal, tier);\n','');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Player/PunchPresentation.cs');s=p.read_text(encoding='utf-8-sig').replace('镜头震动、命中顿帧、准星反馈（本项目不使用音效）。','镜头震动、命中顿帧、准星反馈及共享音效/特效。');s=s.replace('    private Camera view;','    private GameActions.LevelPlayerPresentation polished;\n    private GameActions.LevelPlayerPresentation Polished => polished ? polished : polished = GameActions.LevelPlayerPresentation.Ensure(gameObject);\n    private Camera view;');s=s.replace('        Shake(.12f, .009f + tier * .004f);','        Shake(.12f, .009f + tier * .004f);\n        Polished.Swing(tier);');s=s.replace('public void Impact(Vector3 point, Vector3 normal, int tier, bool flesh)','public void Impact(Vector3 point, Vector3 normal, int tier, bool flesh, Collider target = null)');s=s.replace('        DemoImpactBurst.Spawn(point, normal, tier, flesh);','        Polished.Contact(target, point, normal);');s=s.replace('            DemoImpactBurst.Spawn(spawnPos, spawnNormal, 2, false);','            Polished.Guard(true, spawnPos, spawnNormal);');s=s.replace('''        }
    }

    private void Shake''','''        }
        else if (!success) Polished.Guard(false, transform.position, Vector3.up);
    }

    private void Shake''');p.write_text(s,encoding='utf-8')
# Feedback only after successful state transitions; no alteration of puzzle physics.
p=Path('Assets/Scripts/Puzzle/KineticBox.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        rb = GetComponent<Rigidbody>();','        GameActions.PuzzlePresentation.Ensure(gameObject);\n        rb = GetComponent<Rigidbody>();',1);s=s.replace('        isMomentumLocked = true;','        isMomentumLocked = true;\n        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Lock);',1);s=s.replace('        CheckMovingBoxPlayerCollision();\n    }\n\n    public void SetBulletTimeHighlight','        CheckMovingBoxPlayerCollision();\n        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Release);\n    }\n\n    public void SetBulletTimeHighlight');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Puzzle/DashCrystal.cs');s=p.read_text(encoding='utf-8-sig').replace('            respawnTimer = respawnCooldown;','            GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Recharge);\n            respawnTimer = respawnCooldown;');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Puzzle/PuzzleSwitch.cs');s=p.read_text(encoding='utf-8-sig').replace('        onActivated.Invoke();','        onActivated.Invoke();\n        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Mechanism);');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Puzzle/PuzzleButton.cs');s=p.read_text(encoding='utf-8-sig').replace('        onPressed.Invoke();','        onPressed.Invoke();\n        GameActions.PuzzlePresentation.Play(this, GameActions.PuzzleFeedback.Mechanism);');p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/Shared/Actions/Runtime/SurfaceAudio.cs');s=p.read_text(encoding='utf-8-sig').replace('            if(c.GetComponentInParent<CrystalParkour.CrystalSurface>())','            if(c.GetComponentInParent<KineticBox>()||c.GetComponentInParent<PendulumMechanism>()||c.GetComponentInParent<Cannon>())return SoundSurface.Metal;\n            if(c.GetComponentInParent<DashCrystal>())return SoundSurface.Crystal;\n            if(c.GetComponentInParent<PlayerCombatController>()||c.GetComponentInParent<FirstPerson.FirstPersonVitals>())return SoundSurface.Flesh;\n            if(c.GetComponentInParent<CrystalParkour.CrystalSurface>())');p.write_text(s,encoding='utf-8')
