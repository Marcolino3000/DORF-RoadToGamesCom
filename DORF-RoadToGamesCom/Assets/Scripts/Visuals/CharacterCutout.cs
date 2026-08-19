using UnityEngine;

/// <summary>
/// Feeds the "Coming of Dorf/Sprite Occluder Cutout" shader with the character's position and size
/// on screen. Every sprite using that shader opens a soft hole there, but only where it actually
/// stands in front of her, so she never disappears behind a tree or a fence.
/// </summary>
[DisallowMultipleComponent]
public class CharacterCutout : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Extra room around the character, as a fraction of her size on screen.")]
    [Range(0f, 2f)] [SerializeField] private float padding = 0.4f;
    [Tooltip("Smallest hole radius in viewport units, so it never collapses to nothing.")]
    [Range(0f, 0.5f)] [SerializeField] private float minRadius = 0.02f;
    [Tooltip("How long the hole takes to catch up with the character. 0 = instant.")]
    [Range(0f, 0.3f)] [SerializeField] private float smoothing = 0.05f;

    [Header("References")]
    [Tooltip("Root of the character. Her world Z decides which sprites count as 'in front'.")]
    [SerializeField] private Transform character;
    [Tooltip("Sprites that make up the character. Filled from the character root when left empty.")]
    [SerializeField] private Renderer[] characterRenderers;
    [SerializeField] private Camera cam;

    [Header("Debug")]
    [SerializeField] private bool cutoutActive = true;
    [SerializeField] private Vector2 currentCenter;
    [SerializeField] private Vector2 currentRadius;

    private static readonly int CenterId = Shader.PropertyToID("_CodCutoutCenter");
    private static readonly int RadiusId = Shader.PropertyToID("_CodCutoutRadius");

    private Vector2 centerVelocity;
    private Vector2 radiusVelocity;
    private bool isFollowing;

    private void Awake()
    {
        if (character == null)
            character = transform;

        if (characterRenderers == null || characterRenderers.Length == 0)
            characterRenderers = character.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnDisable()
    {
        ClearCutout();
    }

    private void LateUpdate()
    {
        if (!cutoutActive || character == null)
        {
            ClearCutout();
            return;
        }

        if (cam == null)
            cam = Camera.main;

        if (cam == null || !TryGetVisibleBounds(out Bounds bounds))
        {
            ClearCutout();
            return;
        }

        Vector3 center = cam.WorldToViewportPoint(bounds.center);
        if (center.z <= 0f) // behind the camera
        {
            ClearCutout();
            return;
        }

        // Project her extents through the same camera instead of guessing a radius, so the hole
        // keeps matching her on-screen size when the camera moves closer or further away.
        Vector3 side = cam.WorldToViewportPoint(bounds.center + Vector3.right * bounds.extents.x);
        Vector3 top = cam.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y);

        Vector2 targetCenter = new Vector2(center.x, center.y);
        Vector2 targetRadius = new Vector2(
            Mathf.Max(Mathf.Abs(side.x - center.x) * (1f + padding), minRadius),
            Mathf.Max(Mathf.Abs(top.y - center.y) * (1f + padding), minRadius));

        if (isFollowing && smoothing > 0f)
        {
            currentCenter = Vector2.SmoothDamp(currentCenter, targetCenter, ref centerVelocity, smoothing);
            currentRadius = Vector2.SmoothDamp(currentRadius, targetRadius, ref radiusVelocity, smoothing);
        }
        else
        {
            currentCenter = targetCenter;
            currentRadius = targetRadius;
            centerVelocity = Vector2.zero;
            radiusVelocity = Vector2.zero;
            isFollowing = true;
        }

        Shader.SetGlobalVector(CenterId, new Vector4(currentCenter.x, currentCenter.y, character.position.z, 1f));
        Shader.SetGlobalVector(RadiusId, new Vector4(currentRadius.x, currentRadius.y, 0f, 0f));
    }

    private bool TryGetVisibleBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasAny = false;

        foreach (Renderer characterRenderer in characterRenderers)
        {
            if (characterRenderer == null || !characterRenderer.enabled) continue;
            if (!characterRenderer.gameObject.activeInHierarchy) continue;

            if (hasAny)
            {
                bounds.Encapsulate(characterRenderer.bounds);
            }
            else
            {
                bounds = characterRenderer.bounds;
                hasAny = true;
            }
        }

        return hasAny;
    }

    /// <summary>Closes the hole. The w component is the "cutout is active" flag in the shader.</summary>
    private void ClearCutout()
    {
        isFollowing = false;
        Shader.SetGlobalVector(CenterId, Vector4.zero);
    }
}
