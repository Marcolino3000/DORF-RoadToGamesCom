using System;
using System.Collections;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Scrolls the layered landscape that passes the train window in Scene 1.
    ///
    /// Every layer is a quad holding one strip of the "landschaft neu" panorama, all of them cut
    /// from the same painting: bottoms flush at world Y 235.92, sized at 38 panorama pixels per
    /// world unit (quad scale = texture pixels / 38). Keep that scale if layers are re-added, or
    /// the strips no longer line up.
    ///
    /// Only the speed differs. The trackside forest whips past, the sky barely moves — that is what
    /// sells the distance. <see cref="Layer.parallaxFactor"/> 1 is the village band, the layer that
    /// carries the perceived train speed, so <see cref="scrollSpeed"/> reads as the train's speed.
    /// </summary>
    public class Landscape : MonoBehaviour
    {
        [Serializable]
        private class Layer
        {
            public MeshRenderer meshRenderer;

            [Tooltip("Speed relative to the village layer. Nearer to the camera = higher. 0 pins " +
                     "the layer in place.")]
            [Range(0f, 4f)] public float parallaxFactor = 1f;
        }

        [Header("Settings")]
        [Tooltip("World units per second travelled by the layer with parallaxFactor 1.")]
        [SerializeField] private float scrollSpeed;
        [SerializeField] private float slowDownDuration;

        [Header("Offset cues")]
        [Tooltip("Panorama pixels per world unit — the scale the layer quads are sized at. Only " +
                 "used so offset cues can be stated in the painting's own pixels rather than in " +
                 "world units or UV.")]
        [SerializeField] private float pixelsPerUnit = 38f;

        [Tooltip("Layer whose scroll position OffsetPixels reports, indexed into the list below. " +
                 "1 is the village layer, the one that reads as the train's position.")]
        [SerializeField] private int cueLayerIndex = 1;

        [Header("Sorting")]
        [Tooltip("Order in Layer written to every layer's MeshRenderer. Everything else in the " +
                 "scene sits at order 0, so anything below that keeps the panorama behind it.")]
        [SerializeField] private int sortingOrder = -1;

        [Header("Layers, near to far")]
        [SerializeField] private Layer[] layers;

        private Material[] materials;

        // UV offset per world unit travelled, per layer. One UV unit spans the whole quad divided
        // by the tiling, so this bakes each layer's own width out of the speed — rescale a quad in
        // the inspector and it still travels at scrollSpeed.
        private float[] offsetsPerUnit;

        private Coroutine slowDownCoroutine;

        // The authored scroll speed, kept so SpeedFactor still reads 1 at full pelt after
        // SlowDown() has written scrollSpeed down. Captured before anything can brake.
        private float fullScrollSpeed;

        private void Awake()
        {
            fullScrollSpeed = scrollSpeed;
        }

        /// <summary>
        /// Current scroll speed as a fraction of the authored one: 1 while the train runs, ramping
        /// to 0 across <see cref="SlowDown"/>. Anything that moves *with* the train rather than
        /// merely alongside it — Marlene rocking in her seat — takes its own speed from this
        /// instead of watching for the brake itself.
        /// </summary>
        public float SpeedFactor => fullScrollSpeed > 0f ? scrollSpeed / fullScrollSpeed : 0f;

        private void Start()
        {
            ApplySortingOrder();

            materials = new Material[layers.Length];
            offsetsPerUnit = new float[layers.Length];

            for (int i = 0; i < layers.Length; i++)
            {
                MeshRenderer meshRenderer = layers[i].meshRenderer;
                if (meshRenderer == null)
                {
                    Debug.LogWarning($"Landscape layer {i} has no MeshRenderer and will not scroll.");
                    continue;
                }

                // .material, not .sharedMaterial: this must not write the scroll offset back into
                // the asset on disk, or every play session starts wherever the last one stopped.
                materials[i] = meshRenderer.material;

                float quadWidth = meshRenderer.transform.lossyScale.x;
                offsetsPerUnit[i] = quadWidth > 0f
                    ? materials[i].mainTextureScale.x / quadWidth
                    : 0f;
            }
        }

        /// <summary>
        /// Puts the whole panorama behind everything else in the scene, once and for good.
        ///
        /// The Scene 1 camera is perspective and left on the default transparency sort, so Unity
        /// orders transparent renderers by straight-line distance from the camera position rather
        /// than by depth. A window mask sitting far out to the side therefore counts as *further
        /// away* than the landscape it is meant to cover, even when it is metres nearer in Z, and
        /// the panorama paints over it. Sorting layer and order are compared before that distance,
        /// so a negative order settles the question wherever the camera ends up.
        /// </summary>
        private void ApplySortingOrder()
        {
            if (layers == null) return;

            foreach (Layer layer in layers)
            {
                if (layer?.meshRenderer == null) continue;
                if (layer.meshRenderer.sortingOrder == sortingOrder) continue;

                layer.meshRenderer.sortingOrder = sortingOrder;
            }
        }

        // Also from OnValidate, so the Game view matches a build without entering play mode. The
        // order is serialised on the renderers, so it sticks once the scene is saved.
        private void OnValidate()
        {
            ApplySortingOrder();
        }

        private void Update()
        {
            ScrollLayers();
        }

        private void ScrollLayers()
        {
            float travelled = scrollSpeed * Time.deltaTime;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                Vector2 offset = materials[i].mainTextureOffset;

                // Wrapped rather than accumulated: the kiosk runs for hours, and a float offset
                // that only ever grows loses the small per-frame increments.
                offset.x = Mathf.Repeat(
                    offset.x + travelled * layers[i].parallaxFactor * offsetsPerUnit[i], 1f);

                materials[i].mainTextureOffset = offset;
            }
        }

        private bool CueLayerReady =>
            materials != null && cueLayerIndex >= 0 && cueLayerIndex < materials.Length
            && materials[cueLayerIndex] != null && offsetsPerUnit[cueLayerIndex] > 0f;

        /// <summary>Width of the cue layer's panorama in its own pixels — the offset wraps here.</summary>
        public float PanoramaWidthPixels =>
            CueLayerReady ? pixelsPerUnit / offsetsPerUnit[cueLayerIndex] : 0f;

        /// <summary>
        /// How far the cue layer's panorama has scrolled, in panorama pixels counted from its left
        /// edge. Wraps back to 0 at <see cref="PanoramaWidthPixels"/>.
        /// </summary>
        public float OffsetPixels =>
            CueLayerReady
                ? materials[cueLayerIndex].mainTextureOffset.x * PanoramaWidthPixels
                : 0f;

        /// <summary>
        /// Waits until the cue layer has scrolled to <paramref name="targetPixels"/>. Always waits
        /// forwards: a mark the panorama has just gone past costs a full lap rather than passing
        /// straight through, so the landscape is reliably at the same place afterwards.
        /// </summary>
        /// <param name="timeoutSeconds">
        /// Gives up and returns after this long. The kiosk must not hang on a mark it cannot reach
        /// — if the scroll has been stopped, the mark never arrives. 0 waits indefinitely.
        /// </param>
        public IEnumerator WaitForOffsetPixels(float targetPixels, float timeoutSeconds)
        {
            float width = PanoramaWidthPixels;
            if (width <= 0f)
            {
                Debug.LogWarning($"Landscape has no usable cue layer at index {cueLayerIndex}, " +
                                 $"not waiting for offset {targetPixels}px.");
                yield break;
            }

            float remaining = Mathf.Repeat(targetPixels - OffsetPixels, width);
            float waited = 0f;

            while (remaining > 0f)
            {
                float before = OffsetPixels;
                yield return null;

                waited += Time.deltaTime;
                if (timeoutSeconds > 0f && waited >= timeoutSeconds)
                {
                    Debug.LogWarning($"Landscape did not reach offset {targetPixels}px within " +
                                     $"{timeoutSeconds}s ({remaining:F0}px short); continuing.");
                    yield break;
                }

                // Repeat, so the frame the offset wraps on still counts as forward progress.
                remaining -= Mathf.Repeat(OffsetPixels - before, width);
            }
        }

        [ContextMenu("Log Offset")]
        private void LogOffset()
        {
            Debug.Log($"Landscape offset: {OffsetPixels:F0} of {PanoramaWidthPixels:F0} px " +
                      $"(layer {cueLayerIndex}). Only meaningful in play mode.");
        }

        [ContextMenu("Slow Down")]
        public void SlowDown()
        {
            if (slowDownCoroutine != null)
            {
                StopCoroutine(slowDownCoroutine);
            }

            slowDownCoroutine = StartCoroutine(SlowDownRoutine());
        }

        private IEnumerator SlowDownRoutine()
        {
            float startSpeed = scrollSpeed;
            float timeElapsed = 0f;

            while (timeElapsed < slowDownDuration)
            {
                scrollSpeed = Mathf.Lerp(startSpeed, 0f, timeElapsed / slowDownDuration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            scrollSpeed = 0f;
        }
    }
}
