using UnityEngine;

/// <summary>
/// Marks this object as an occluder: its sprites get the cutout material and open up around the
/// character when she walks behind them. Everything without it keeps its normal material.
///
/// A single sprite can just as well get "Sprite Occluder Cutout" assigned directly in its
/// SpriteRenderer - that also shows the effect in the scene view. This component is for covering a
/// whole group of foreground objects with one drag.
/// </summary>
[DisallowMultipleComponent]
public class CutoutOccluder : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Also switch every sprite below this object, not just the ones on it.")]
    [SerializeField] private bool includeChildren = true;

    [Header("References")]
    [SerializeField] private Material occluderMaterial;

    [Header("Debug")]
    [SerializeField] private int switchedRenderers;

    private void Awake()
    {
        if (occluderMaterial == null)
        {
            Debug.LogError($"{nameof(CutoutOccluder)} on '{name}' has no occluder material.", this);
            return;
        }

        SpriteRenderer[] renderers = includeChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponents<SpriteRenderer>();

        switchedRenderers = renderers.Length;

        foreach (SpriteRenderer spriteRenderer in renderers)
            spriteRenderer.sharedMaterial = occluderMaterial;
    }
}
