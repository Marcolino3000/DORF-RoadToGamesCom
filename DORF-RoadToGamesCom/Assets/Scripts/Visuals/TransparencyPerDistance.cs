using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using UnityEngine;

public class TransparencyPerDistance : MonoBehaviour
{
    [Header("Settings")] 
    [SerializeField] private bool objectFadesWhenClose;
    // [SerializeField] private float maxTransparancyThreshold;
    [SerializeField] private float colliderEnabledThreshold;
    
    [Header("Debug")]
    [SerializeField] private float currentTransparency;
    [SerializeField] private PlayerController player;
    [SerializeField] private bool playerIsNear;

    [Header("References")]
    // [SerializeField] private Interactable interactable;
    [SerializeField] private Collider collider;
    [SerializeField] private TriggerArea triggerArea;
    [SerializeField] private SpriteRenderer doorRenderer;

    private float colliderRadius;
    // cached property id for shader color to avoid string lookups
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void OnEnable()
    {
        doorRenderer = GetComponent<SpriteRenderer>();
            
        if(triggerArea == null)
            triggerArea = gameObject.GetComponentInChildren<TriggerArea>();
        
        var triggerCollider = triggerArea.GetComponent<SphereCollider>();

        if (triggerCollider != null)
            colliderRadius = triggerCollider.radius;

        triggerArea.OnPlayerEntered -= HandlePlayerEntered;
        triggerArea.OnPlayerExited -= HandlePlayerExited;

        triggerArea.OnPlayerEntered += HandlePlayerEntered;
        triggerArea.OnPlayerExited += HandlePlayerExited;

        // A play session that ended in the dead ring can have left this off on the asset.
        if (collider != null)
            collider.enabled = true;
    }

    // Rooms are switched by activating and deactivating them, so OnEnable runs again every time
    // Marlene walks back in. Without this the handler list grows for the rest of the play-through.
    private void OnDisable()
    {
        if (triggerArea == null)
            return;

        triggerArea.OnPlayerEntered -= HandlePlayerEntered;
        triggerArea.OnPlayerExited -= HandlePlayerExited;
    }

    private void HandlePlayerExited()
    {
        playerIsNear = false;
        player = null;
    }

    private void HandlePlayerEntered(PlayerController player)
    {
        playerIsNear = true;
        this.player = player;
    }

    private void Update()
    {
        if (!playerIsNear) return;

        SetTransparency();
    }

    private void SetTransparency()
    {

        // measure horizontal distance only (ignore Y)
        float distance = Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(transform.position.x, transform.position.z)
        );

        // account for non-uniform scaling: use the largest lossyScale component
        float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        // guard against zero scale/radius
        float maxDistance = Mathf.Max(0.0001f, colliderRadius * maxScale);
        
        // normalize distance to [0,1]
        float normalized = Mathf.InverseLerp(0f, maxDistance, distance);

        float alpha = objectFadesWhenClose ? normalized : 1f - normalized;

        var color = doorRenderer.color;
        color.a = alpha;
        currentTransparency = alpha;
        doorRenderer.color = color;
        
        // if(interactable != null)
        // {
        //     if (alpha < minTransparancyThreshold)
        //         interactable.enabled = false;
        //
        //     if (alpha > minTransparancyThreshold)
        //         interactable.enabled = true;
        // }
        
        // The collider is deliberately NOT driven from the alpha any more. It is the door's only
        // clickable surface, and the fade band and the walk-up distance contradict each other: the
        // trigger sphere reaches 2.90 units, the alpha drops under colliderEnabledThreshold beyond
        // 2.61, and InteractionStarter parks Marlene 2.0 units from the interactable. Stopping
        // anywhere in that ring switched the door off exactly while she stood in front of it - and
        // the only code that could switch it back on is this method, which stops running the moment
        // she leaves the trigger. Standing further away worked, standing close did not.
    }
    

    private void SetTransparencyInShader()
    {
        // measure horizontal distance only (ignore Y)
        float distance = Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(transform.position.x, transform.position.z)
        );

        float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float maxDistance = Mathf.Max(0.0001f, colliderRadius * maxScale);

        float normalized = Mathf.InverseLerp(0f, maxDistance, distance);
        float alpha = objectFadesWhenClose ? Mathf.Clamp01(normalized) : Mathf.Clamp01(1f - normalized);

        var color = doorRenderer.sharedMaterial.GetColor(ColorId);
        color.a = alpha;
        currentTransparency = alpha;
        doorRenderer.material.SetColor(ColorId, color);
    }
}
