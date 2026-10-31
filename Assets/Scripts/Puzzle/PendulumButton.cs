using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Button on the player platform that releases the giant East-West pendulum.
/// Can be activated by pressing F (when nearby or looking at it) or by dashing/stepping onto it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PendulumButton : MonoBehaviour
{
    [SerializeField] private PendulumMechanism targetPendulum;
    [SerializeField] private SeesawMechanism targetSeesaw;
    [SerializeField] private float interactDistance = 4.5f;
    [SerializeField] private Color readyColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.95f, 0.3f, 1f);

    private Renderer buttonRenderer;
    private MaterialPropertyBlock propBlock;
    private Transform playerTransform;

    private void Awake()
    {
        buttonRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            playerTransform = player.transform;

        bool isCraneButton = gameObject.name.Contains("Crane");
        bool isPendulumButton = gameObject.name.Contains("Pendulum");

        if (targetPendulum == null && !isCraneButton)
            targetPendulum = FindFirstObjectByType<PendulumMechanism>();

        if (targetSeesaw == null && !isPendulumButton)
            targetSeesaw = FindFirstObjectByType<SeesawMechanism>();

        UpdateVisuals();
    }

    private void Update()
    {
        if (targetPendulum == null && targetSeesaw == null)
            return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame && playerTransform != null)
        {
            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist <= interactDistance)
            {
                TriggerTarget();
            }
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            TriggerTarget();
        }
    }

    private void TriggerTarget()
    {
        if (targetPendulum != null)
            targetPendulum.TriggerRelease();

        if (targetSeesaw != null)
            targetSeesaw.TriggerCraneRelease();
    }

    private void UpdateVisuals()
    {
        if (buttonRenderer == null)
            return;

        bool isReady = (targetPendulum != null && targetPendulum.IsReady) ||
                       (targetSeesaw != null && targetSeesaw.IsReady);

        Color c = isReady ? readyColor : activeColor;
        buttonRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", c);
        propBlock.SetColor("_Color", c);
        buttonRenderer.SetPropertyBlock(propBlock);
    }
}
