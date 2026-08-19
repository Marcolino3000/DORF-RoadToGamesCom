using Runtime.Scripts.Utility;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flipbook that rides on the surface of the dough in the Sauerteig jar. All ten frames draw the
/// blob on the same baseline and differ only in their upper edge, so playing them back moves the
/// surface. The animation itself is never scaled - it only travels up and down with the top edge of
/// the dough image, which is the one that keeps stretching.
/// </summary>
[RequireComponent(typeof(Image))]
public class SauerteigDoughAnimation : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Sauerteig-1 .. Sauerteig-10, in that order.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 12f;
    [Tooltip("Below this much change in the dough's localScale.y per frame it counts as standing still.")]
    [SerializeField] private float scaleEpsilon = 0.0001f;

    [Header("References")]
    [Tooltip("The dough image that stretches. Its top edge is what this animation follows. It is " +
             "only ever read, never moved - its height belongs to SauerteigStatusDisplay.")]
    [SerializeField] private RectTransform dough;

    [Header("Placement")]
    [Tooltip("Distance from the top edge of the dough. The pivot sits on the bottom of the drawn " +
             "blob, so 0 parks the animation on top of the dough and negative values sink it in.")]
    [SerializeField] private float verticalOffset = -64.1f;

    [Header("Preview")]
    [Tooltip("How often the preview runs through the ten frames.")]
    [SerializeField] private int previewLoops = 2;
    [Tooltip("Plays the frames in the scene view, no play mode needed. Click again to stop early.")]
    [InspectorButton(nameof(PreviewAnimation))] [SerializeField] private bool previewAnimation;

    [Header("Debug")]
    [SerializeField] private int frameIndex;
    [SerializeField] private bool isPlaying;

    private RectTransform rectTransform;
    private Image image;
    private float lastScaleY;
    private float frameTimer;

    private void Awake()
    {
        CacheReferences();

        if (frames == null || frames.Length == 0)
        {
            Debug.LogError($"{nameof(SauerteigDoughAnimation)} on '{name}' has no frames.", this);
            enabled = false;
            return;
        }

        if (dough == null)
        {
            Debug.LogError($"{nameof(SauerteigDoughAnimation)} on '{name}' has no dough to follow.", this);
            enabled = false;
            return;
        }

        lastScaleY = dough.localScale.y;

        ShowFrame(0);
        FollowDoughTop();
    }

    private void Update()
    {
        var scaleY = dough.localScale.y;
        var delta = scaleY - lastScaleY;
        lastScaleY = scaleY;

        FollowDoughTop();

        isPlaying = Mathf.Abs(delta) > scaleEpsilon;

        if (!isPlaying)
            return;

        // growing runs the rise forwards, shrinking plays it back down
        var direction = delta > 0 ? 1 : -1;

        frameTimer += Time.deltaTime * framesPerSecond;

        while (frameTimer >= 1f)
        {
            frameTimer -= 1f;
            ShowFrame(frameIndex + direction);
        }
    }

    /// <summary>
    /// Parks the animation on the upper edge of the dough. Both hang off StretchySauerteig, so they
    /// share one anchored space and no world round trip is needed.
    /// </summary>
    private void FollowDoughTop()
    {
        var doughTop = dough.anchoredPosition.y
                       + dough.rect.height * (1f - dough.pivot.y) * dough.localScale.y;

        var position = rectTransform.anchoredPosition;
        position.y = doughTop + verticalOffset;
        rectTransform.anchoredPosition = position;
    }

    private void ShowFrame(int index)
    {
        frameIndex = (index % frames.Length + frames.Length) % frames.Length;
        image.sprite = frames[frameIndex];
    }

    private void CacheReferences()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
    }

    /// <summary>
    /// Runs the flipbook straight from the Inspector, in the scene view and without entering play
    /// mode, and puts the sprite back afterwards so no preview frame ends up saved in the prefab.
    /// Clicking again stops it early.
    /// </summary>
    [ContextMenu("Preview Animation")]
    public void PreviewAnimation()
    {
#if UNITY_EDITOR
        if (previewRunning)
        {
            StopPreview();
            return;
        }

        CacheReferences();

        if (frames == null || frames.Length == 0)
        {
            Debug.LogError($"{nameof(SauerteigDoughAnimation)} on '{name}' has no frames to preview.", this);
            return;
        }

        previewSprite = image.sprite;
        previewStepsLeft = Mathf.Max(1, previewLoops) * frames.Length;
        previewNextStep = UnityEditor.EditorApplication.timeSinceStartup;
        previewRunning = true;

        UnityEditor.EditorApplication.update += StepPreview;
        // a recompile mid-preview would drop the update hook and leave the preview frame behind
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
#endif
    }

#if UNITY_EDITOR
    private double previewNextStep;
    private int previewStepsLeft;
    private bool previewRunning;
    private Sprite previewSprite;

    private void StepPreview()
    {
        if (this == null)
        {
            UnityEditor.EditorApplication.update -= StepPreview;
            return;
        }

        var now = UnityEditor.EditorApplication.timeSinceStartup;

        if (now < previewNextStep)
            return;

        previewNextStep = now + 1d / Mathf.Max(0.01f, framesPerSecond);

        ShowFrame(frameIndex + 1);
        UnityEditor.SceneView.RepaintAll();

        if (--previewStepsLeft <= 0)
            StopPreview();
    }

    private void StopPreview()
    {
        UnityEditor.EditorApplication.update -= StepPreview;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        previewRunning = false;

        if (this == null)
            return;

        image.sprite = previewSprite;
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        if (previewRunning)
            StopPreview();
    }
#endif
}
