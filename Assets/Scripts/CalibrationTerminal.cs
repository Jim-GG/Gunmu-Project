using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// A simple calibration panel; successful confirmation fires once per scene load.
public class CalibrationTerminal : DemoInteractable
{
    [SerializeField, Range(0f, 360f)] private float initialDialAngle = 60f;
    [SerializeField, Range(0f, 360f)] private float targetAngle = 240f;
    [SerializeField, Range(1f, 45f)] private float tolerance = 15f;
    [SerializeField, Min(0.1f)] private float requiredStableTime = 0.8f;
    [SerializeField] private UnityEvent onCalibrated = new UnityEvent();

    private float dialAngle;
    private float stableTime;
    private PlayerInteractor activePlayer;
    private bool isOpen;
    public bool IsSolved { get; private set; }
    public override string Prompt => IsSolved ? "[E] Calibration complete" : "[E] Calibrate terminal";

    private void Awake()
    {
        dialAngle = initialDialAngle;
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return base.CanInteract(interactor) && !isOpen && !interactor.IsUIOpen;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (IsSolved)
        {
            interactor.ShowFeedback("Already calibrated.");
            return;
        }
        if (!interactor.BeginUI(this)) return;
        activePlayer = interactor;
        stableTime = 0f;
        isOpen = true;
    }

    private void Update()
    {
        if (!isOpen) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
            return;
        }
        if (Application.isFocused && IsAligned())
            stableTime = Mathf.Min(requiredStableTime, stableTime + Time.deltaTime);
        else
            stableTime = 0f;
    }

    private bool IsAligned()
    {
        return Mathf.Abs(Mathf.DeltaAngle(dialAngle, targetAngle)) <= tolerance;
    }

    private void ClosePanel()
    {
        isOpen = false;
        stableTime = 0f;
        if (activePlayer != null) activePlayer.EndUI(this);
        activePlayer = null;
    }

    private void ConfirmCalibration()
    {
        if (IsSolved || !IsAligned() || stableTime < requiredStableTime) return;
        IsSolved = true;
        PlayerInteractor player = activePlayer;
        ClosePanel();
        if (player != null) player.ShowFeedback("Calibration complete!", 3f);
        onCalibrated.Invoke();
    }

    private void OnDisable()
    {
        if (isOpen) ClosePanel();
    }

    private void OnGUI()
    {
        if (!isOpen) return;
        GUI.depth = -50;
        Matrix4x4 oldMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 800f, Screen.height / 500f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        Rect panel = new Rect((Screen.width / scale - 640f) / 2f,
            (Screen.height / scale - 360f) / 2f, 640f, 360f);
        GUI.Box(panel, "");
        GUIStyle title = new GUIStyle(GUI.skin.label)
            { fontSize = 25, alignment = TextAnchor.MiddleCenter };
        GUIStyle text = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x, panel.y + 20f, 640f, 40f), "RESONANCE CALIBRATION", title);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 70f, 600f, 35f),
            "Move the dial into the green zone and hold it steady.", text);
        float oldAngle = dialAngle;
        dialAngle = GUI.HorizontalSlider(new Rect(panel.x + 55f, panel.y + 125f, 530f, 25f),
            dialAngle, 0f, 360f);
        if (!Mathf.Approximately(oldAngle, dialAngle)) stableTime = 0f;
        Rect rail = new Rect(panel.x + 55f, panel.y + 160f, 530f, 16f);
        DrawRect(rail, Color.gray);
        for (int shift = -1; shift <= 1; shift++)
        {
            float left = Mathf.Clamp01((targetAngle + 360f * shift - tolerance) / 360f);
            float right = Mathf.Clamp01((targetAngle + 360f * shift + tolerance) / 360f);
            if (right > left)
                DrawRect(new Rect(rail.x + left * rail.width, rail.y,
                    (right - left) * rail.width, rail.height), new Color(0.2f, 0.8f, 0.35f));
        }
        DrawRect(new Rect(rail.x + dialAngle / 360f * rail.width - 2f,
            rail.y - 4f, 4f, rail.height + 8f), Color.white);
        bool ready = IsAligned() && stableTime >= requiredStableTime;
        GUI.Label(new Rect(panel.x + 20f, panel.y + 195f, 600f, 35f),
            ready ? "STABLE - confirm calibration" :
            $"Dial: {dialAngle:0} degrees | Stable: {stableTime:0.0}/{requiredStableTime:0.0}s", text);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = ready;
        if (GUI.Button(new Rect(panel.x + 70f, panel.y + 260f, 230f, 50f), "Confirm"))
            ConfirmCalibration();
        GUI.enabled = oldEnabled;
        if (GUI.Button(new Rect(panel.x + 340f, panel.y + 260f, 230f, 50f), "Close (Esc)"))
            ClosePanel();
        GUI.matrix = oldMatrix;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}
