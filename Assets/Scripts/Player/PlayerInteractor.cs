using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to Player; aim at a nearby interactable and press E.
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.1f)] private float interactionRange = 2.8f;

    private DemoInteractable target;
    private string feedback;
    private float feedbackUntil;
    private GUIStyle promptStyle;
    public Camera ViewCamera => viewCamera;
    public bool IsUIOpen => modalOwner != null;
    private DemoInteractable modalOwner;
    private MonoBehaviour[] suspendedControls;
    private bool[] previousEnabled;
    private bool closingUI;

    private void Start()
    {
        if (viewCamera == null)
        {
            Debug.LogError("PlayerInteractor: assign View Camera in the Inspector.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        target = null;
        if (IsUIOpen || Cursor.lockState != CursorLockMode.Locked)
            return;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && health.IsDefeated)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return;

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;
            DemoInteractable candidate = hit.collider.GetComponentInParent<DemoInteractable>();
            if (candidate != null && candidate.CanInteract(this))
                target = candidate;
            // The first solid object blocks interaction with anything behind it.
            break;
        }

        if (target != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            target.Interact(this);
    }

    public void ShowFeedback(string message, float duration = 2f)
    {
        feedback = message;
        feedbackUntil = Time.time + duration;
    }

    public bool BeginUI(DemoInteractable owner)
    {
        if (IsUIOpen || owner == null) return false;
        modalOwner = owner;
        target = null;
        suspendedControls = new MonoBehaviour[] {
            GetComponent<PlayerMovement>(), GetComponent<PlayerLook>(),
            GetComponent<PlayerGauntlet>() };
        previousEnabled = new bool[suspendedControls.Length];
        for (int i = 0; i < suspendedControls.Length; i++)
        {
            if (suspendedControls[i] == null) continue;
            previousEnabled[i] = suspendedControls[i].enabled;
            suspendedControls[i].enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public void EndUI(DemoInteractable owner)
    {
        if (modalOwner != owner || closingUI) return;
        closingUI = true;
        StartCoroutine(RestoreControls());
    }

    private IEnumerator RestoreControls()
    {
        // Consume the closing click/Esc before restoring gameplay controls.
        yield return null;
        PlayerHealth health = GetComponent<PlayerHealth>();
        bool alive = health == null || !health.IsDefeated;
        for (int i = 0; i < suspendedControls.Length; i++)
            if (suspendedControls[i] != null)
                suspendedControls[i].enabled = alive && previousEnabled[i];
        modalOwner = null;
        closingUI = false;
        bool lockCursor = alive && Application.isFocused;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }

    private void OnDisable()
    {
        target = null;
    }

    private void OnGUI()
    {
        if (promptStyle == null)
            promptStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        PlayerGauntlet weapon = GetComponent<PlayerGauntlet>();
        if (!IsUIOpen && (weapon == null || !weapon.enabled)
            && Cursor.lockState == CursorLockMode.Locked)
            GUI.Label(new Rect(Screen.width / 2f - 12f, Screen.height / 2f - 18f, 24f, 36f),
                "+", promptStyle);
        if (target != null && Cursor.lockState == CursorLockMode.Locked)
            GUI.Label(new Rect(10f, Screen.height - 125f, Screen.width - 20f, 35f),
                target.Prompt, promptStyle);
        if (Time.time < feedbackUntil)
            GUI.Label(new Rect(10f, Screen.height - 170f, Screen.width - 20f, 35f),
                feedback, promptStyle);
    }
}
