using UnityEngine;

/// <summary>
/// Switch inside the narrow gap that activates when struck by a KineticBox.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuzzleSwitch : MonoBehaviour
{
    [SerializeField] private Color inactiveColor = new Color(1f, 0.35f, 0.1f, 1f);
    [SerializeField] private Color activeColor = new Color(0.15f, 1f, 0.35f, 1f);
    [SerializeField] private Transform doorOrIndicatorToMove;
    [SerializeField] private Vector3 activatedOffset = new Vector3(0f, 2.5f, 0f);

    private Renderer switchRenderer;
    private MaterialPropertyBlock propBlock;
    private Vector3 doorInitialPos;
    private bool isActivated;

    public bool IsActivated => isActivated;

    private void Awake()
    {
        switchRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        if (doorOrIndicatorToMove == null && transform.parent != null)
        {
            Transform found = transform.parent.Find("VictoryGate_East");
            if (found == null)
                found = transform.parent.Find("GapWall_MidBar");
            if (found != null)
                doorOrIndicatorToMove = found;
        }

        if (doorOrIndicatorToMove != null)
            doorInitialPos = doorOrIndicatorToMove.position;

        UpdateVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<KineticBox>() != null || other.GetComponent<PlayerMovement>() != null)
            Activate();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<KineticBox>() != null || collision.collider.GetComponent<PlayerMovement>() != null)
            Activate();
    }

    public void Activate()
    {
        if (isActivated)
            return;

        isActivated = true;
        UpdateVisuals();

        if (doorOrIndicatorToMove != null)
            doorOrIndicatorToMove.position = doorInitialPos + activatedOffset;

        Debug.Log("[PuzzleSwitch] Switch Activated! Billiard Puzzle Solved!", this);
    }

    public void ResetSwitch()
    {
        isActivated = false;
        UpdateVisuals();

        if (doorOrIndicatorToMove != null)
            doorOrIndicatorToMove.position = doorInitialPos;
    }

    private void UpdateVisuals()
    {
        if (switchRenderer == null)
            return;

        Color targetColor = isActivated ? activeColor : inactiveColor;
        switchRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", targetColor);
        propBlock.SetColor("_Color", targetColor);
        switchRenderer.SetPropertyBlock(propBlock);
    }
}
