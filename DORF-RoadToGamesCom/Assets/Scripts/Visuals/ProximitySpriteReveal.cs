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

    [Header("Debug")]
    [SerializeField] private float currentAlpha;
    [SerializeField] private PlayerController player;
    [SerializeField] private bool playerIsNear;

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

        ApplyAlpha(farAlpha); // start hidden until the player approaches
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
        player = null;
        playerIsNear = false;
        ApplyAlpha(farAlpha); // fully faded once the player leaves the area
    }

    private void Update()
    {
        if (!playerIsNear) return;

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
        float alpha = Mathf.Lerp(closeAlpha, farAlpha, normalized);

        ApplyAlpha(alpha);
    }

    private void ApplyAlpha(float alpha)
    {
        currentAlpha = alpha;

        // this outline sprite shader has no _Color property; it reads alpha from the
        // vertex color, which the SpriteRenderer feeds from its own color
        Color color = baseColor;
        color.a = alpha;
        spriteRenderer.color = color;

        // drop the collider once the sprite has faded past the threshold
        if (interactionCollider != null)
            interactionCollider.enabled = alpha >= colliderEnabledThreshold;
    }
}
