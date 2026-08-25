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

        // Rooms come back with whatever the last visit left on them. Update corrects this within
        // a frame; starting from "clickable" is the harmless direction to be wrong in.
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
        // The reference stays: it is only used to measure the distance, and holding on to it saves
        // looking Marlene up again on every re-entry.
        playerIsNear = false;
    }

    private void HandlePlayerEntered(PlayerController player)
    {
        playerIsNear = true;
        this.player = player;
    }

    private void Update()
    {
        UpdateInteractionCollider();

        if (!playerIsNear) return;

        SetTransparency();
    }

    /// <summary>
    /// Outside the house the front door is the only way in, so it stays clickable from anywhere in
    /// the garden. Inside it lies across the Hallway and would swallow clicks meant for the room
    /// behind it, so there only proximity counts.
    ///
    /// Measured, not remembered: playerIsNear comes from OnTriggerEnter, and that event goes missing
    /// often enough - SequenceRunner switches Marlene's collider off for every scripted walk, and
    /// RoomManager deactivates whole rooms underneath her. This collider is the door's only clickable
    /// surface, so hanging it off the event left a single missed enter to kill the door for the rest
    /// of the play-through.
    ///
    /// The radius is the trigger sphere's own, not an alpha threshold: those two disagreed, and the
    /// ring between them was exactly where InteractionStarter parks Marlene when she walks up.
    /// </summary>
    private void UpdateInteractionCollider()
    {
        if (collider == null)
            return;

        if (RoomManager.IsOutside)
        {
            collider.enabled = true;
            return;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        // Without Marlene there is nothing to measure against. Leave it on: a door that cannot be
        // clicked locks the visitor in, an oversized click surface costs nothing.
        if (player == null)
        {
            collider.enabled = true;
            return;
        }

        // Measured against the collider's own object, not this outline: that object carries the
        // Interactable too, so it is the exact point InteractionStarter walks Marlene to. She stops
        // 2.0 units short of it (MoveByClick.interactionStoppingDistance) and the trigger sphere
        // reaches 2.90, so standing in front of the door is always inside the radius - the ring that
        // used to switch the door off under her cannot come back.
        collider.enabled = GetPlayerDistanceTo(collider.transform.position) <= GetTriggerWorldRadius();
    }

    private float GetPlayerDistanceTo(Vector3 worldPosition)
    {
        // measure horizontal distance only (ignore Y), matching the gameplay plane
        return Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(worldPosition.x, worldPosition.z)
        );
    }

    private float GetTriggerWorldRadius()
    {
        // account for non-uniform scaling: use the largest lossyScale component
        float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        // guard against zero scale/radius
        return Mathf.Max(0.0001f, colliderRadius * maxScale);
    }

    private void SetTransparency()
    {
        // normalize distance to [0,1] - the outline keeps fading around its own object, and shares
        // the radius with the collider so the two cannot drift apart again
        float normalized = Mathf.InverseLerp(0f, GetTriggerWorldRadius(), GetPlayerDistanceTo(transform.position));

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
        
        // The collider is not driven from here - see UpdateInteractionCollider. Alpha is the wrong
        // handle for it: the trigger sphere reaches 2.90 units, the alpha used to drop under
        // colliderEnabledThreshold beyond 2.61, and InteractionStarter parks Marlene 2.0 units from
        // the interactable. Stopping anywhere in that ring switched the door off exactly while she
        // stood in front of it, and only this method could have switched it back on - which stops
        // running the moment she leaves the trigger. colliderEnabledThreshold is unused since.
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
