using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ProximitySpriteReveal : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Alpha when the player stands right on the object.")]
    [Range(0f, 1f)] [SerializeField] private float closeAlpha = 1f;
    [Tooltip("Alpha at the edge of the trigger and while the player is away.")]
    [Range(0f, 1f)] [SerializeField] private float farAlpha = 0;
    [Tooltip("When the alpha drops below this, the 2D collider is switched off (0 = never disable it).")]
    [Range(0f, 1f)] [SerializeField] private float colliderEnabledThreshold;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TriggerArea triggerArea;
    [SerializeField] private Collider2D interactionCollider;
    [Tooltip("Optional. While this door is toggled open the outline stays hidden - the collider is not affected.")]
    [SerializeField] private Toggleable doorToggleable;

    [Header("Debug")]
    [SerializeField] private float currentAlpha;
    [SerializeField] private PlayerController player;
    [SerializeField] private bool playerIsNear;
    [SerializeField] private bool isActive;

    private SphereCollider triggerCollider;
    private Color baseColor;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // the trigger area is expected to sit on this object (or a child of it)
        if (triggerArea == null)
            triggerArea = GetComponentInChildren<TriggerArea>();

        if (triggerArea == null)
        {
            Debug.LogError($"{nameof(ProximitySpriteReveal)} on '{name}' has no {nameof(TriggerArea)}.", this);
            enabled = false;
            return;
        }

        triggerCollider = triggerArea.GetComponent<SphereCollider>();

        // keep the renderer's rgb, we only drive the alpha
        baseColor = spriteRenderer.color;

        Refresh();
    }

    private void OnEnable()
    {
        if (triggerArea == null) return;

        triggerArea.OnPlayerEntered += HandlePlayerEntered;
        triggerArea.OnPlayerExited += HandlePlayerExited;
    }

    private void OnDisable()
    {
        if (triggerArea == null) return;

        triggerArea.OnPlayerEntered -= HandlePlayerEntered;
        triggerArea.OnPlayerExited -= HandlePlayerExited;
    }

    private void HandlePlayerEntered(PlayerController enteringPlayer)
    {
        player = enteringPlayer;
        playerIsNear = true;
    }

    private void HandlePlayerExited()
    {
        // The reference stays: it is only used to measure the distance, and holding on to it saves
        // looking Marlene up again on every re-entry.
        playerIsNear = false;
    }

    // everything is derived from the current state every frame, so neither a missed
    // callback nor an unlucky event order can leave the sprite stuck at a stale alpha
    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        float proximityAlpha = GetProximityAlpha();

        // the outline is only a hint for the closed door. Read the toggle live: InteractableState
        // raises OnInteractionFeedback *before* Toggleable.Toggle() flips ToggleState, so caching
        // it in an event handler always yields the previous state.
        isActive = doorToggleable == null || !doorToggleable.ToggleState;

        currentAlpha = isActive ? proximityAlpha : farAlpha;

        Color color = baseColor;
        color.a = currentAlpha;
        spriteRenderer.color = color;

        // the collider follows the distance only, never the toggle: this sprite is the door's
        // only clickable surface, so hiding the outline must never lock the player out of it
        if (interactionCollider != null)
            interactionCollider.enabled = proximityAlpha >= colliderEnabledThreshold;
    }

    private float GetProximityAlpha()
    {
        if (triggerCollider == null)
            return farAlpha;

        // Measured, not remembered. playerIsNear comes from OnTriggerEnter, and that event goes
        // missing often enough: SequenceRunner switches Marlene's collider off for every scripted
        // walk, and RoomManager deactivates whole rooms underneath her. The interaction collider
        // below hangs off this value, and the only way back into the trigger is to click that very
        // collider - so a single missed event used to leave the door dead for the rest of the
        // play-through, with the collider being switched off again every frame.
        // Outside the radius InverseLerp clamps to 1 and this returns farAlpha anyway, so the
        // reading is the same as before wherever the event was correct.
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null)
            return farAlpha;

        // measure horizontal distance only (ignore Y), matching the gameplay plane
        float distance = Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(transform.position.x, transform.position.z)
        );

        // world-space radius of the trigger sphere, accounting for its scale
        Vector3 triggerScale = triggerCollider.transform.lossyScale;
        float worldRadius = triggerCollider.radius * Mathf.Max(triggerScale.x, triggerScale.z);
        worldRadius = Mathf.Max(0.0001f, worldRadius); // guard against zero radius/scale

        // 0 at the centre, 1 at the edge -> closer means more visible
        float normalized = Mathf.InverseLerp(0f, worldRadius, distance);

        return Mathf.Lerp(closeAlpha, farAlpha, normalized);
    }
}
