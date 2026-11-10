from pathlib import Path
p=Path('Assets/Game/Player/Runtime/GauntletCombat.cs');s=p.read_text(encoding='utf-8-sig').replace('if(!animationPlayer||!animationPlayer.animator||!profile)return false;','if(!animationPlayer||!animationPlayer.gameObject.activeInHierarchy||!animationPlayer.animator||!profile)return false;');p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/Shared/Actions/Runtime/LevelPlayerPresentation.cs');s=p.read_text(encoding='utf-8-sig').replace('SwingsPlayed++; sounds.Play(','SwingsPlayed++; if(!GetComponent<FirstPerson.GauntletCombat>())sounds.Play(').replace('GuardsPlayed++; sounds.Play(','GuardsPlayed++; if(!GetComponent<FirstPerson.GauntletCombat>())sounds.Play(');p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/Shared/Actions/Runtime/GauntletChargeAudio.cs');s=p.read_text(encoding='utf-8-sig').replace('stages.clip.length / duration * state.PlaybackSpeed, .1f, 3','stages.clip.length / duration * state.PlaybackSpeed * Time.timeScale, .05f, 3');p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/Enemies/Resonator/AI/Runtime/ResonatorTarget.cs');s=p.read_text(encoding='utf-8-sig').replace('combat.ReceiveAttack(hit.source, Mathf.Max(1, Mathf.RoundToInt(hit.damage)))','combat.ReceiveAttack(hit.source, hit.damage, hit.blockable)').replace('return blocked ? StrikeResult.Blocked : StrikeResult.Damaged;','return combat.LastGuard == GameActions.GuardKind.Perfect ? StrikeResult.Blocked : StrikeResult.Damaged;');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Enemies/ResonatorPunchTarget.cs');s=p.read_text(encoding='utf-8-sig').replace('    public bool IsFlesh => true;','    public bool IsFlesh => true;\n    public bool OverridesAuthoredDamage => tutorialLaunch;');p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/Enemies/Resonator/AI/Runtime/ResonatorEnemy.cs');s=p.read_text(encoding='utf-8-sig').replace('public void ReceiveActionDamage(GameActions.ActionDamage hit)=>ApplyDamage(hit);','''public void ReceiveActionDamage(GameActions.ActionDamage hit)
        {
            var tutorial=GetComponent<ResonatorPunchTarget>();
            if(tutorial&&tutorial.OverridesAuthoredDamage){tutorial.OnPunched(transform.position,Vector3.up,hit.direction,hit.chargeLevel,hit.knockback);return;}
            ApplyDamage(hit);
        }''');p.write_text(s,encoding='utf-8')
