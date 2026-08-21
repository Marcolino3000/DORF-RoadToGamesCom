using Runtime.Scripts.Utility;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flipbook that rides on the surface of the dough in the Sauerteig jar. All ten frames draw the
/// blob on the same baseline and differ only in their upper edge, so playing them back moves the
/// surface. The animation itself is never scaled.
///
/// Nothing here moves the Sauerteig upwards. The blob hangs off the *lower* edge of the dough, not
/// its upper one, so the fill level rising underneath no longer carries the animation with it and
/// the flipbook works the surface in place - park it on the first frame and it stands exactly where
/// it stood before the run. <see cref="followDoughTop"/> puts the old behaviour back.
///
/// <see cref="pinDoughBottom"/> holds that lower edge itself where it started while
/// <see cref="SauerteigStatusDisplay"/> scales the dough, so the base cannot drift either.
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
    [Tooltip("The dough image that stretches. Its top edge is what this animation follows. Its " +
             "height belongs to SauerteigStatusDisplay and is never touched here - only its " +
             "position is, and only while Pin Dough Top is on.")]
    [SerializeField] private RectTransform dough;

    [Header("Placement")]
    [Tooltip("Distance from the top edge of the dough, read once on Awake to work out where the " +
             "blob rests. The pivot sits on the bottom of the drawn blob, so 0 parks the animation " +
             "on top of the dough and negative values sink it in.")]
    [SerializeField] private float verticalOffset = -64.1f;

    [Tooltip("Rides the upper edge of the dough, so the animation climbs along as the fill level " +
             "grows. Off by default - the blob is pinned to the lower edge instead and stays put.")]
    [SerializeField] private bool followDoughTop;

    [Tooltip("Keeps the lower edge of the dough where it stood on Awake while " +
             "SauerteigStatusDisplay scales it, so the dough grows out of a fixed base and never " +
             "slides as a whole.")]
    [SerializeField] private bool pinDoughBottom = true;

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
    private int direction = 1;
    private float pinnedDoughBottom;
    private float restingPlacement;

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
        pinnedDoughBottom = DoughBottom();
        restingPlacement = DoughTop() + verticalOffset;

        ShowFrame(0);
        PlaceOnDough();
    }

    private void Update()
    {
        if (pinDoughBottom)
            HoldDoughBottom();

        var scaleY = dough.localScale.y;
        var delta = scaleY - lastScaleY;
        lastScaleY = scaleY;

        isPlaying = Mathf.Abs(delta) > scaleEpsilon;

        if (isPlaying)
            // growing runs the rise forwards, shrinking plays it back down
            direction = delta > 0 ? 1 : -1;

        if (isPlaying || frameIndex != 0)
            AdvanceFrames();
        else
            // parked on the first frame, so the next move starts on a whole frame
            frameTimer = 0f;

        PlaceOnDough();
    }

    private void AdvanceFrames()
    {
        frameTimer += Time.deltaTime * framesPerSecond;

        while (frameTimer >= 1f)
        {
            frameTimer -= 1f;
            ShowFrame(frameIndex + direction);

            // the dough came to rest mid-flipbook, so keep running in the last direction until
            // the loop is back on the first frame and stop there
            if (!isPlaying && frameIndex == 0)
            {
                frameTimer = 0f;
                break;
            }
        }
    }

    /// <summary>
    /// Keeps the lower edge of the dough on the height it started at, whatever its pivot and
    /// current scale are. Only the position is written - the scale stays with
    /// SauerteigStatusDisplay, and reading it is still what drives the flipbook.
    /// </summary>
    private void HoldDoughBottom()
    {
        var position = dough.anchoredPosition;
        position.y = pinnedDoughBottom + dough.rect.height * dough.pivot.y * dough.localScale.y;
        dough.anchoredPosition = position;
    }

    /// <summary>
    /// Puts the animation back on its placement. Both it and the dough hang off StretchySauerteig,
    /// so they share one anchored space and no world round trip is needed. Pinned to the lower edge
    /// of the dough it keeps the height it was authored at and only shifts if that edge shifts,
    /// which is what stops the flipbook from travelling upwards.
    /// </summary>
    private void PlaceOnDough()
    {
        var position = rectTransform.anchoredPosition;

        position.y = followDoughTop
            ? DoughTop() + verticalOffset
            : restingPlacement + DoughBottom() - pinnedDoughBottom;

        rectTransform.anchoredPosition = position;
    }

    private float DoughTop()
    {
        return dough.anchoredPosition.y
               + dough.rect.height * (1f - dough.pivot.y) * dough.localScale.y;
    }

    private float DoughBottom()
    {
        return dough.anchoredPosition.y - dough.rect.height * dough.pivot.y * dough.localScale.y;
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
