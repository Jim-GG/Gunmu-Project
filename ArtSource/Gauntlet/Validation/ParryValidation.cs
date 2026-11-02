using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FirstPerson;

public static class ParryValidation
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static object Assets()
    {
        var combat = UnityEngine.Object.FindFirstObjectByType<GauntletCombat>();
        var clips = combat.animationPlayer.animator.runtimeAnimatorController.animationClips;
        foreach (var name in new[]{"Parry-Start","Parry-Keep","Parry-Hit","Parry-Holster"})
        {
            Check(combat.animationPlayer.animator.HasState(0, Animator.StringToHash(name)), "Missing state " + name);
            var clip = clips.First(c => c.name == name);
            Check(clip.isLooping == (name == "Parry-Keep"), "Loop setting " + name);
            Check(AnimationUtility.GetCurveBindings(clip).All(b => b.path == "" || combat.animationPlayer.animator.transform.Find(b.path)), "Unbound curves " + name);
        }
        var p = new ChargePolicy();
        foreach (int initial in new[]{0,1,2,3})
        {
            int energy = initial, mask = 0;
            float single = p.Advance(0, .8f, ref energy, ref mask, out int skipped);
            int energy2 = initial, mask2 = 0; float stepped = 0;
            for (int i = 0; i < 120; i++) stepped = p.Advance(stepped, .8f / 120, ref energy2, ref mask2, out _);
            Check(Mathf.Abs(single - stepped) < .0001f && energy == energy2, "Low FPS skip mismatch " + initial);
            Check(energy >= 0 && skipped <= initial, "Energy accounting");
        }
        return new { passed = true, parryClips = 4, largeDeltaMatches120Steps = true };
    }
    public static object Runtime()
    {
        Check(Application.isPlaying, "Enter Play Mode first");
        var c = UnityEngine.Object.FindFirstObjectByType<GauntletCombat>();
        var input = c.GetComponent<FirstPersonInput>(); var motor = c.GetComponent<FirstPersonMotor>();
        var enemy = UnityEngine.Object.FindFirstObjectByType<ParryTrainingEnemy>();
        var vitals = c.GetComponent<FirstPersonVitals>(); var animator = c.animationPlayer.animator;
        bool ie = input.enabled, me = motor.enabled, ce = c.enabled, ee = enemy.enabled;
        input.enabled = motor.enabled = c.enabled = enemy.enabled = false;
        var records = new List<string>();
        int punch = 0; Action<int> onPunch = n => punch = n; c.AttackStarted += onPunch;
        Action<float> step = dt => { animator.Update(dt); c.Tick(dt); };
        Action finish = () => { for (int i = 0; i < 45; i++) step(1/30f); };
        Action<int> seed = n =>
        {
            c.Cancel(); c.Press(); c.Tick(4); c.Cancel(); // consume any existing energy
            c.BeginBlock();
            for (int i = 0; i < n; i++) Check(c.TryBlock(c.transform.position + c.transform.forward * 2), "Seed guard");
            c.EndBlock(); finish();
            Check(c.BlockEnergy == n && c.State == GauntletCombat.Phase.Idle, "Seed stacks " + n);
        };
        try
        {
            seed(0); c.BeginBlock(); Check(c.IsBlocking && c.State == GauntletCombat.Phase.ParryStart, "Guard immediate");
            Check(c.TryBlock(c.transform.position + c.transform.forward * 2), "Start interrupted by hit");
            Check(c.State == GauntletCombat.Phase.ParryHit && c.BlockEnergy == 1, "Hit state/energy");
            finish(); Check(c.State == GauntletCombat.Phase.ParryKeep, "Held hit returns keep");
            animator.Update(5); c.Tick(5); Check(c.State == GauntletCombat.Phase.ParryKeep, "Keep must loop");
            int before = c.BlockEnergy; Check(!c.TryBlock(c.transform.position - c.transform.forward * 2) && c.BlockEnergy == before, "Back hit must fail");
            for (int i = 0; i < 5; i++) c.TryBlock(c.transform.position + c.transform.forward * 2);
            Check(c.BlockEnergy == 3, "Energy cap"); c.EndBlock(); Check(c.State == GauntletCombat.Phase.ParryHolster && !c.IsBlocking, "Release interrupts hit");
            finish(); Check(c.State == GauntletCombat.Phase.Idle, "Holster finishes");
            c.BeginBlock(); finish(); Check(c.State == GauntletCombat.Phase.ParryKeep, "Start to keep"); c.EndBlock(); finish();
            c.Press(); c.Release(); c.BeginBlock(); Check(c.State == GauntletCombat.Phase.Punching, "Attack queues guard"); finish(); Check(c.State == GauntletCombat.Phase.ParryStart || c.State == GauntletCombat.Phase.ParryKeep, "Queued guard starts"); c.EndBlock(); finish();
            for (int level = 0; level <= 3; level++)
            {
                seed(level); c.Press(); c.Tick(13/30f);
                int frame = c.policy.Frame(c.ChargeSeconds);
                Check(frame == (level > 0 ? 31 : 13), "First skip " + level + " got " + frame);
                if (level > 0)
                {
                    animator.Update(0); var state = animator.GetCurrentAnimatorStateInfo(0);
                    Check(state.IsName("charge") && Mathf.Abs(state.normalizedTime * state.length * 30 - frame) < .05f, "Animator did not seek");
                }
                Check(c.BlockEnergy == Mathf.Max(0,level-1), "First cost");
                c.Tick((33-frame)/30f); frame = c.policy.Frame(c.ChargeSeconds);
                Check(frame == (level > 1 ? 61 : 33) && c.BlockEnergy == Mathf.Max(0,level-2), "Second skip " + level);
                c.Tick((63-frame)/30f); frame = c.policy.Frame(c.ChargeSeconds);
                Check(frame == (level > 2 ? 91 : 63) && c.BlockEnergy == 0, "Third skip " + level);
                c.Tick((92-frame)/30f); step(.05f); Check(c.State == GauntletCombat.Phase.Full, "Full " + level);
                c.Release(); Check(punch == 4, "Full release punch4"); finish();
                records.Add("energy " + level + ": correct destinations, costs, full -> punch4");
            }
            seed(3); c.Press(); c.Tick(.1f); c.Release(); Check(punch == 1 && c.BlockEnergy == 3, "Quick click preserves stacks"); finish();
            c.Press(); c.Tick(.2f); c.Release(); c.Tick(.24f); Check(punch == 2 && c.BlockEnergy == 3, "Early queued release preserves stacks"); finish();
            seed(0); vitals.Restore(); float health = vitals.Health;
            Check(!enemy.Strike() && vitals.Health == health - enemy.damage, "Unblocked enemy damage");
            c.BeginBlock(); float hurt = vitals.Health; int hits = enemy.AttacksBlocked;
            enemy.Tick(enemy.recoverySeconds + 1); enemy.Tick(enemy.windupSeconds + .01f);
            Check(enemy.AttacksBlocked > hits && c.BlockEnergy == 1 && vitals.Health == hurt, "Telegraphed enemy must grant energy and prevent damage");
            return new { passed = true, cases = records.ToArray(), parryLifecycle = true, animatorSeek = true, quickAndQueuedReleasePreserveEnergy = true, realEnemyBlocked = true, enemyHits = enemy.AttacksLanded };
        }
        finally
        {
            c.AttackStarted -= onPunch; c.Cancel(); vitals.Restore();
            c.enabled = ce; motor.enabled = me; input.enabled = ie; enemy.enabled = ee;
        }
    }
}
