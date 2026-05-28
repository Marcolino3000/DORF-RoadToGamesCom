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
        
        colliderRadius = triggerArea.GetComponent<SphereCollider>().radius;
        triggerArea.OnPlayerEntered += HandlePlayerEntered;
        triggerArea.OnPlayerExited += HandlePlayerExited;
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
        
        if(collider != null)
        {
            if (alpha < colliderEnabledThreshold)
                collider.enabled = false;

            if (alpha > colliderEnabledThreshold)
                collider.enabled = true;
        }
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
