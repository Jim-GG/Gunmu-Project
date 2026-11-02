using System;
using UnityEngine;

namespace FirstPerson
{
    [Serializable]
    public sealed class ChargePolicy
    {
        public float framesPerSecond = 30f, quickClickSeconds = .12f;
        public int releaseGate = 13, tierOne = 32, tierTwo = 62, full = 92;
        public int Frame(float seconds) => Mathf.FloorToInt(seconds * framesPerSecond + .0001f);
        public int Punch(float seconds) => seconds <= quickClickSeconds ? 1 : Frame(seconds) < tierTwo ? 2 : Frame(seconds) < full ? 3 : 4;
        public int Tier(float seconds) => Frame(seconds) >= full ? 3 : Frame(seconds) >= tierTwo ? 2 : Frame(seconds) >= tierOne ? 1 : 0;

        // Add the skipped duration to preserve the remainder of this tick, even at low FPS.
        // A visited interval cannot consume another stack during the same charge.
        public float Advance(float seconds, float dt, ref int energy, ref int visited, out int skipped)
        {
            float next = seconds + Mathf.Max(0, dt);
            skipped = 0;
            for (int stage = 0; stage < 3; stage++)
            {
                int start = stage == 0 ? 13 : stage == 1 ? 33 : 63;
                int destination = stage == 0 ? 31 : stage == 1 ? 61 : 91;
                if ((visited & (1 << stage)) != 0 || Frame(next) < start) continue;
                visited |= 1 << stage;
                if (energy <= 0 || Frame(seconds) >= destination) continue;
                float remaining = Mathf.Max(0, destination / framesPerSecond - Mathf.Max(seconds, start / framesPerSecond));
                next += remaining;
                energy--; skipped++;
            }
            return next;
        }
    }

    public sealed class GauntletCombat : MonoBehaviour
    {
        public enum Phase { Idle, Charging, ReleasePending, Full, Punching, ParryStart, ParryKeep, ParryHit, ParryHolster }
        public Gauntlet.GauntletAnimationPlayer animationPlayer;
        public ChargePolicy policy = new ChargePolicy();
        [Range(0, 360)] public float blockAngle = 120f;
        public string fullState = "charge full";
        public bool externalInputEnabled = true;
        public Phase State { get; private set; }
        public float ChargeSeconds { get; private set; }
        public int ChargeTier => policy.Tier(ChargeSeconds);
        public int BlockEnergy { get; private set; }
        public bool IsBlocking => State == Phase.ParryStart || State == Phase.ParryKeep || State == Phase.ParryHit;
        public event Action<int> TierChanged, AttackStarted, AttackFinished, EnergyChanged, ChargeSkipped;
        public event Action AttackBlocked;
        private bool held, blockHeld;
        private float heldSeconds;
        private int lastTier, activePunch, visitedSkips;

        public void Press()
        {
            if (!externalInputEnabled || State != Phase.Idle || blockHeld || !animationPlayer) return;
            held = true; heldSeconds = ChargeSeconds = 0; lastTier = visitedSkips = 0;
            State = Phase.Charging;
            animationPlayer.PlayControlled("charge", 0f);
        }
        public void Release()
        {
            held = false;
            if (State != Phase.Charging && State != Phase.Full) return;
            if (heldSeconds <= policy.quickClickSeconds) { Attack(1); return; }
            if (policy.Frame(ChargeSeconds) < policy.releaseGate) State = Phase.ReleasePending;
            else Attack(policy.Punch(ChargeSeconds));
        }
        public void BeginBlock()
        {
            if (!externalInputEnabled || !animationPlayer) return;
            blockHeld = true;
            // An attack finishes before a queued guard starts. Guard can cancel charging.
            if (State == Phase.Punching || IsBlocking) return;
            held = false; ChargeSeconds = 0; animationPlayer.SetCharge(0);
            EnterParry(Phase.ParryStart, "Parry-Start");
        }
        public void EndBlock()
        {
            blockHeld = false;
            if (IsBlocking) EnterParry(Phase.ParryHolster, "Parry-Holster");
        }
        public bool TryBlock(Vector3 attackerPosition)
        {
            if (!IsBlocking) return false;
            var direction = Vector3.ProjectOnPlane(attackerPosition - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .0001f && Vector3.Angle(transform.forward, direction) > blockAngle * .5f) return false;
            SetEnergy(BlockEnergy + 1);
            EnterParry(Phase.ParryHit, "Parry-Hit");
            AttackBlocked?.Invoke();
            return true;
        }
        private void EnterParry(Phase phase, string clip)
        {
            State = phase;
            // Immediate interrupts: release and repeated hits must not wait for a transition.
            animationPlayer.PlayControlled(clip, 0f);
        }
        private void SetEnergy(int value)
        {
            value = Mathf.Clamp(value, 0, 3);
            if (value == BlockEnergy) return;
            BlockEnergy = value; EnergyChanged?.Invoke(value);
        }
        private bool Finished(string clip)
        {
            var animator = animationPlayer.animator;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(clip) && state.normalizedTime >= 1f && !animator.IsInTransition(0);
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (State == Phase.Idle || !animationPlayer) return;
            if (State == Phase.Punching)
            {
                if (Finished("punch" + activePunch))
                {
                    int completed = activePunch;
                    ReturnIdle(); AttackFinished?.Invoke(completed);
                    if (blockHeld) EnterParry(Phase.ParryStart, "Parry-Start");
                }
                return;
            }
            if (IsBlocking || State == Phase.ParryHolster)
            {
                if ((State == Phase.ParryStart && Finished("Parry-Start")) || (State == Phase.ParryHit && Finished("Parry-Hit")))
                    EnterParry(blockHeld ? Phase.ParryKeep : Phase.ParryHolster, blockHeld ? "Parry-Keep" : "Parry-Holster");
                else if (State == Phase.ParryHolster && Finished("Parry-Holster")) ReturnIdle();
                return;
            }
            if (State == Phase.Full) return;
            dt = Mathf.Max(0, dt); heldSeconds += dt;
            if (State == Phase.ReleasePending)
            {
                ChargeSeconds += dt;
                if (policy.Frame(ChargeSeconds) >= policy.releaseGate) { Attack(2); return; }
            }
            else
            {
                int energy = BlockEnergy;
                ChargeSeconds = policy.Advance(ChargeSeconds, dt, ref energy, ref visitedSkips, out int skipped);
                if (skipped > 0)
                {
                    SetEnergy(energy);
                    animationPlayer.SeekControlled("charge", ChargeSeconds);
                    ChargeSkipped?.Invoke(skipped);
                }
            }
            animationPlayer.SetCharge(Mathf.Clamp01(ChargeSeconds * policy.framesPerSecond / policy.full));
            int tier = ChargeTier;
            if (tier != lastTier) { lastTier = tier; TierChanged?.Invoke(tier); }
            if (State == Phase.Charging && held && policy.Frame(ChargeSeconds) >= policy.full)
            { State = Phase.Full; animationPlayer.PlayControlled(fullState, .04f); }
        }
        private void Attack(int punch)
        {
            activePunch = punch; held = false; State = Phase.Punching;
            animationPlayer.PlayControlled("punch" + punch, .025f); AttackStarted?.Invoke(punch);
        }
        private void ReturnIdle()
        {
            State = Phase.Idle; ChargeSeconds = 0;
            animationPlayer.PlayControlled("Idle"); animationPlayer.SetCharge(0);
        }
        public void Cancel()
        {
            held = blockHeld = false; State = Phase.Idle; ChargeSeconds = heldSeconds = 0;
            if (animationPlayer && animationPlayer.animator && animationPlayer.animator.isActiveAndEnabled)
            { animationPlayer.PlayControlled("Idle"); animationPlayer.SetCharge(0); }
        }
        private void OnDisable() => Cancel();
    }
}
