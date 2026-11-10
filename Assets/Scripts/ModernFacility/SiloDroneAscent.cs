using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SiloDroneAscent : MonoBehaviour, ILevelExitGate
{
    [Serializable]
    public sealed class Stage
    {
        public CargoAttackDrone drone;
        public BoxCollider platform;
        public Transform boardingPoint;
        public Transform landingPoint;
    }

    public PlayerMovement player;
    public Stage[] stages;
    public Transform shaftAxis, fanApproach, fanRotor;
    public Collider fanBarrier;
    [Min(0.1f)] public float liftSpeed = 4f;
    public int CurrentStage { get; private set; }
    public bool IsSatisfied => CurrentStage == stages.Length;

    private CharacterController body;
    private PlayerChargedPunch punch;
    private PlayerCombatController combat;
    private Transform checkpoint;
    private Vector3[] route;
    private int waypoint;
    private bool punchWasEnabled, waitingForFan;

    private void Awake()
    {
        body = player.GetComponent<CharacterController>();
        combat = player.GetComponent<PlayerCombatController>();
        punch = player.GetComponent<PlayerChargedPunch>();
        checkpoint = stages[0].boardingPoint;
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked || combat.IsDefeated) return;

        bool riding = route != null;
        Vector3 feet = player.transform.position;
        for (int i = 0; i < stages.Length; i++)
        {
            Bounds deck = stages[i].platform.bounds;
            bool onDeck = feet.x >= deck.min.x && feet.x <= deck.max.x
                && feet.z >= deck.min.z && feet.z <= deck.max.z
                && feet.y >= deck.max.y - .2f && feet.y <= deck.max.y + 1.5f;
            stages[i].drone.CombatEnabled = i == CurrentStage && !riding && onDeck;
        }

        if (riding)
        {
            CarryPlayer();
            return;
        }

        if (feet.y < checkpoint.position.y - 1.6f)
            player.TeleportTo(checkpoint.position);

        if (Keyboard.current?.eKey.wasPressedThisFrame == true)
            TryBoard();
    }

    public bool TryBoard()
    {
        if (GameplayGate.IsBlocked || combat.IsDefeated || IsSatisfied || route != null) return false;
        Stage stage = stages[CurrentStage];
        if (!stage.drone.Defeated || Vector3.Distance(player.transform.position, stage.boardingPoint.position) > 1.8f)
            return false;

        player.TeleportTo(stage.boardingPoint.position + Vector3.up * .8f);
        punchWasEnabled = punch.enabled;
        punch.enabled = false;
        player.enabled = false;
        stage.drone.SetRemote(true);
        stage.drone.GetComponent<SphereCollider>().enabled = false;

        Vector3 start = stage.boardingPoint.position;
        Vector3 end = stage.landingPoint.position;
        float cruiseHeight = end.y + .45f;
        route = new[]
        {
            start + Vector3.up * (cruiseHeight - start.y),
            new Vector3(shaftAxis.position.x, cruiseHeight, shaftAxis.position.z),
            new Vector3(end.x, cruiseHeight, end.z)
        };
        if (CurrentStage == stages.Length - 1 && fanApproach && fanRotor)
        {
            route = new[]
            {
                route[0],
                route[1],
                new Vector3(fanApproach.position.x, cruiseHeight, fanApproach.position.z),
                route[2]
            };
        }
        waypoint = 0;
        stage.drone.Dock(start);
        return true;
    }

    private void CarryPlayer()
    {
        Stage stage = stages[CurrentStage];
        if (waitingForFan)
        {
            if (Keyboard.current?.qKey.wasPressedThisFrame == true)
                player.ToggleBulletTime();

            // Three equally spaced blades. Start crossing near the middle of the open sector.
            if (!player.IsBulletTimeActive) return;
            float travelSeconds = Mathf.Abs(Vector3.Dot(
                fanRotor.position - stage.drone.transform.position, fanRotor.forward)) / liftSpeed;
            float rotation = fanRotor.GetComponent<CrystalParkour.WindFanRotor>().degreesPerSecond;
            float crossingPhase = Mathf.Repeat(fanRotor.localEulerAngles.z + rotation * travelSeconds * Time.timeScale, 120f);
            if (Mathf.Abs(crossingPhase - 60f) > 4f) return;
            waitingForFan = false;
            if (fanBarrier) fanBarrier.enabled = false;
        }

        Vector3 position = Vector3.MoveTowards(stage.drone.transform.position, route[waypoint], liftSpeed * Time.unscaledDeltaTime);
        stage.drone.Dock(position);
        body.Move(position + Vector3.up * .8f - player.transform.position);
        if ((position - route[waypoint]).sqrMagnitude > .0001f) return;
        if (++waypoint < route.Length)
        {
            waitingForFan = route.Length == 4 && waypoint == 3;
            return;
        }

        checkpoint = stage.landingPoint;
        player.TeleportTo(checkpoint.position);
        player.enabled = true;
        punch.enabled = punchWasEnabled;
        combat.RestoreHealth();
        CurrentStage++;
        route = null;
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        string hint = IsSatisfied ? "走到晶石房的紫色平台完成关卡"
            : waitingForFan ? "按 Q 开启子弹时间，等待扇叶间隙"
            : route != null ? "无人机正在载你前进"
            : stages[CurrentStage].drone.Defeated ? "回到紫色登乘点，按 E 搭乘无人机"
            : "格挡无人机射击，再蓄力反击";
        int width = Mathf.Min(Screen.width - 24, 540);
        GUI.Box(new Rect((Screen.width - width) * .5f, 20, width, 56),
            $"巡逻竖井　{Mathf.Min(CurrentStage + 1, stages.Length)} / {stages.Length}\n{hint}");
    }
}




