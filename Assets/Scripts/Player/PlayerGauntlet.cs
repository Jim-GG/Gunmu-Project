using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Prototype gauntlet: frontal guard stores impact; left click releases one charge.
public class PlayerGauntlet : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.1f)] private float punchRange = 2.5f;
    [SerializeField, Min(0.05f)] private float punchCooldown = 0.45f;
    [SerializeField, Min(1)] private int punchDamage = 1;

    [SerializeField, Min(1)] private int chargedDamage = 2;
    [SerializeField, Min(0f)] private float chargedKnockbackSpeed = 7f;
    [SerializeField, Min(1)] private int maxStoredImpulse = 3;
    [SerializeField, Range(1f, 180f)] private float guardAngle = 120f;
    [SerializeField] private int storedImpulse;

    public int StoredImpulse => storedImpulse;

    public bool IsBlocking => isActiveAndEnabled && viewCamera != null
        && Cursor.lockState == CursorLockMode.Locked && cursorWasLocked
        && Time.time >= nextPunchTime
        && Mouse.current != null && Mouse.current.rightButton.isPressed
        && !(Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

    private float nextPunchTime;
    private float feedbackUntil;
    private string feedback = "";
    private bool cursorWasLocked;
    private GUIStyle crosshairStyle;
    private GUIStyle feedbackStyle;

    private void Start()
    {
        storedImpulse = 0;
        if (viewCamera == null)
        {
            Debug.LogError("PlayerGauntlet: assign View Camera in the Inspector.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // A click used to recapture the cursor must not also punch.
        if (!cursorWasLocked || Cursor.lockState != CursorLockMode.Locked)
            return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (Time.time < nextPunchTime)
            return;

        nextPunchTime = Time.time + punchCooldown;
        Punch();
    }

    private void LateUpdate()
    {
        cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
    }

    private void OnDisable()
    {
        cursorWasLocked = false;
    }

    // The enemy calls this at its hit moment, before applying health damage.
    public bool TryBlockAttack(Vector3 attackerPosition)
    {
        if (!IsBlocking)
            return false;
        Vector3 toAttacker = Vector3.ProjectOnPlane(
            attackerPosition - transform.position, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (toAttacker.sqrMagnitude > 0.0001f
            && Vector3.Angle(forward, toAttacker) > guardAngle * 0.5f)
            return false;

        storedImpulse = Mathf.Min(maxStoredImpulse, storedImpulse + 1);
        ShowFeedback($"BLOCKED! Impulse: {storedImpulse}/{maxStoredImpulse}");
        return true;
    }

    private void Punch()
    {
        // Spend one charge on release, including misses; releasing RMB never spends it.
        bool charged = storedImpulse > 0;
        if (charged)
            storedImpulse--;
        int damage = charged ? chargedDamage : punchDamage;
        // Sort hits so walls block punches; ignore only the player's own body.
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, punchRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            DemoEnemyHealth enemy = hit.collider.GetComponentInParent<DemoEnemyHealth>();
            if (enemy != null && !enemy.IsDefeated)
            {
                enemy.TakeDamage(damage);
                if (charged && !enemy.IsDefeated)
                {
                    DemoEnemyKnockback knockback = enemy.GetComponent<DemoEnemyKnockback>();
                    if (knockback != null)
                        knockback.Push(viewCamera.transform.forward, chargedKnockbackSpeed);
                }
                ShowFeedback(enemy.IsDefeated ? "Enemy defeated!" :
                    $"{(charged ? "IMPACT" : "Hit")}! Enemy HP: {enemy.CurrentHealth}/{enemy.MaxHealth}");
            }
            else
            {
                ShowFeedback("Blocked by object");
            }
            return;
        }

        ShowFeedback(charged ? "Impact missed - charge spent" : "Miss - move closer and aim at the enemy");
    }

    private void ShowFeedback(string message)
    {
        feedback = message;
        feedbackUntil = Time.time + 1.5f;
    }

    private void OnGUI()
    {
        if (viewCamera == null)
            return;

        if (crosshairStyle == null)
        {
            crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
        }

        GUI.Label(new Rect(Screen.width / 2f - 15f, Screen.height / 2f - 15f,
            30f, 30f), "+", crosshairStyle);
        GUI.Label(new Rect(10f, 10f, 700f, 30f), "Hold RMB: guard | LMB: punch / release impulse | Esc: cursor");
        GUI.Label(new Rect(15f, 45f, 600f, 35f),
            $"Impulse: {storedImpulse}/{maxStoredImpulse} | {(IsBlocking ? "GUARDING (front only)" : "Guard down")}");
        if (Time.time < feedbackUntil)
            GUI.Label(new Rect(0f, Screen.height / 2f + 35f, Screen.width, 40f),
                feedback, feedbackStyle);
    }
}
