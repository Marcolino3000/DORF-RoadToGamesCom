using System;
using System.Collections;
using System.Collections.Generic;
using Nodes;
using Runtime.Scripts.PlayerInput;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ScenesSwitches
{
    /// <summary>
    /// Full screen image that covers the game whenever the first scene starts — on launch and on
    /// every restart GameResetter triggers — and fades out to reveal the scene once a visitor picks
    /// a language. Lives on the Global prefab, which the Bootstrapper spawns before the first scene
    /// loads, so the image is already up on frame one.
    ///
    /// The language buttons are the only way past this screen: picking one is what sets
    /// <see cref="Node.CurrentLanguage"/>, and that assignment is what builds the dialog paragraphs
    /// for the play-through. No visitor can end up in a language they did not choose.
    ///
    /// The canvas is built in code: only the artwork has to be delivered. It comes as a stack of
    /// layers painted on one canvas — sky, clouds, landscape, dust, light — which are sized as a
    /// single block so they stay registered with each other on any screen. Three of them carry the
    /// movement — the cloud strip drifts sideways and wraps around forever, the dust sways inside a
    /// margin of its own, and the light rays fade up and down. The rest stands still.
    /// </summary>
    public class StartSplash : MonoBehaviour
    {
        public enum ImageFit
        {
            /// <summary>Fills the screen and crops whatever does not fit.</summary>
            Cover,
            /// <summary>Shows the whole image and leaves bars in the background colour.</summary>
            Contain,
            /// <summary>Distorts the image to the screen aspect.</summary>
            Stretch,
        }

        [Header("Settings")]
        [SerializeField] private string firstSceneName = "Scene 1";
        [SerializeField] private float fadeOutDuration = 1f;
        [Tooltip("The buttons stay dead for this long after the image appears, so the click that restarted the game cannot pick a language along with it.")]
        [SerializeField] private float inputDelay = 0.5f;
        [SerializeField] private ImageFit fit = ImageFit.Cover;
        [Tooltip("Fills whatever the image does not cover — the running scene must never show through.")]
        [SerializeField] private Color backgroundColor = Color.black;
        [Tooltip("Above every other canvas in the game, including the scene fader.")]
        [SerializeField] private int sortingOrder = 999;
        [SerializeField] private bool debugLogs;

        [Header("Image")]
        [Tooltip("Drawn first, behind the clouds — the empty sky.")]
        [SerializeField] private Sprite skyLayer;
        [Tooltip("The one layer that moves. Painted wider than the screen and scrolled sideways, so it has to be set to Repeat in its import settings.")]
        [SerializeField] private Sprite cloudLayer;
        [Tooltip("Standing layers drawn over the clouds, first entry lowest — the landscape.")]
        [SerializeField] private Sprite[] foregroundLayers;
        [Tooltip("Drawn over the landscape and drifting slowly. Sized a little larger than the picture, so the drift cannot pull an empty edge into frame.")]
        [SerializeField] private Sprite dustLayer;
        [Tooltip("Drawn last, over everything, and fading up and down on its own.")]
        [SerializeField] private Sprite lightLayer;
        [Tooltip("Used only when no layer above is assigned: a single image from Assets/Resources/ under this path. The file extension does not matter.")]
        [SerializeField] private string resourcePath = "UI/StartSplash";
        [Tooltip("Until the artwork is delivered, show a generated placeholder so the flow can be tested. Off: no image found means no start screen at all.")]
        [SerializeField] private bool usePlaceholderWhenMissing = true;

        [Header("Clouds")]
        [Tooltip("Seconds a cloud needs to drift across the width of the picture. Higher is slower; a negative value turns the drift around. Zero holds the clouds still.")]
        [SerializeField] private float cloudDriftSeconds = 90f;
        [Tooltip("Top edge of the cloud band, as a fraction of the artwork measured from its top. The strip is delivered cropped to the clouds, so where it hangs in the sky is set here.")]
        [SerializeField] private float cloudBandTop;
        [Tooltip("Bottom edge of the same band. Reaches past the horizon, so the clouds pass behind the landscape instead of ending in mid air.")]
        [SerializeField] private float cloudBandBottom = 0.565f;

        [Header("Dust")]
        [Tooltip("How far the dust drifts either side of its resting place, as a fraction of the picture. The layer is oversized to match, so no amount of drift can show an edge.")]
        [SerializeField] private Vector2 dustDrift = new(0.02f, 0.012f);
        [Tooltip("Seconds for one full round trip, per axis. Two lengths that do not divide into each other, so the pair never settles into a straight back and forth.")]
        [SerializeField] private Vector2 dustDriftSeconds = new(37f, 23f);

        [Header("Light")]
        [Tooltip("How far the light fades back at its dimmest, as a fraction of what was painted. Zero holds it steady.")]
        [SerializeField] private float lightPulse = 0.2f;
        [Tooltip("Seconds for one full fade down and back up.")]
        [SerializeField] private float lightPulseSeconds = 16f;

        [Header("Language")]
        [SerializeField] private Language startingLanguage = Language.De;
        [Tooltip("Leave empty to get a plain plate with the label written on it instead.")]
        [SerializeField] private Sprite germanButtonImage;
        [SerializeField] private Sprite englishButtonImage;
        [Tooltip("Empty hand drawn frame, same look as the main menu. Set this and both buttons get the frame with the label written into it, whatever the two sprites above say.")]
        [SerializeField] private Sprite buttonFrame;
        [Tooltip("The same frame with the glow painted around it. Fades in under the pointer.")]
        [SerializeField] private Sprite buttonFrameGlow;
        [Tooltip("Font for the labels. Leave empty for the TextMeshPro default.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private float labelFontSize = 64f;
        [SerializeField] private string germanLabel = "Deutsch";
        [SerializeField] private string englishLabel = "English";
        [Tooltip("Only used for the plain plate — with a frame the button is exactly as big as the glow sprite, so the strokes stay sharp.")]
        [SerializeField] private Vector2 buttonSize = new(320f, 120f);
        [Tooltip("Gap between the two buttons, in pixels.")]
        [SerializeField] private float buttonSpacing = 60f;
        [Tooltip("Distance from the bottom edge of the screen, in pixels.")]
        [SerializeField] private float buttonBottomMargin = 140f;
        [SerializeField] private Color buttonColor = new(0.09f, 0.09f, 0.11f, 0.85f);
        [SerializeField] private Color buttonTextColor = Color.white;

        /// <summary>
        /// True while the start image covers the screen. GameResetter reads this to stay out of the
        /// way: the start screen is the attract screen already, and the click that starts the game
        /// must not be consumed by both.
        /// </summary>
        public static bool IsShowing { get; private set; }

        /// <summary>
        /// Raised when the start image goes up and when it starts fading away. MusicDirector hangs
        /// the start screen music off these — the screen is a scene object nothing on Global can
        /// hold a reference to, so it announces itself instead.
        /// </summary>
        public static event Action OnShown;

        public static event Action OnHidden;

        private GameObject overlay;
        private CanvasGroup canvasGroup;
        private RectTransform stage;
        private RawImage clouds;
        private Image dust;
        private Image lightRays;
        private float cloudScroll;
        private Vector2 dustPhase;
        private float lightPhase;
        private Coroutine armRoutine;
        private Coroutine fadeRoutine;

        private readonly List<Button> languageButtons = new();

        private void Awake()
        {
            // Runs before the first scene loads, so the game has a defined language from frame one
            // even on the paths where nobody ever picks one — the start screen skipped in the
            // editor, or no start image to show it on. A button press overrides it.
            Node.CurrentLanguage = startingLanguage;

            BuildOverlay();

            SceneManager.sceneLoaded += HandleSceneLoaded;

            // The Bootstrapper creates this before the first scene is loaded, so showing here means
            // no frame of the scene flashes past before the image is up.
            Show();
        }

        private void Start()
        {
            // Entering play mode on any other scene while iterating in the editor must not leave the
            // start image sitting on top of it.
            if (SceneManager.GetActiveScene().name != firstSceneName)
                Hide(instant: true);
        }

        private void Update()
        {
            // Kept running through the fade out — freezing the picture the moment a language is
            // picked would show as a hitch on the way into the game.
            if (overlay == null || !overlay.activeSelf)
                return;

            DriftClouds();
            DriftDust();
            PulseLight();
        }

        /// <summary>
        /// Slides the uv window along the strip. Everything is read back off the rect on screen
        /// rather than counted in pixels, so the clouds stay at their painted size on any screen
        /// and the importer is free to scale the strip down.
        /// </summary>
        private void DriftClouds()
        {
            if (clouds == null)
                return;

            var rect = clouds.rectTransform.rect;
            var texture = clouds.texture;

            if (rect.height <= 0f || texture == null || texture.width <= 0)
                return;

            // How much of the strip fits across the picture: the same ratio however large the
            // screen is, and unaffected by the strip being imported at a smaller size.
            var window = rect.width / rect.height * texture.height / (float)texture.width;

            // A cloud crosses the picture in cloudDriftSeconds, and the picture is exactly one
            // window wide — so a whole strip takes that long divided by the window.
            cloudScroll = Advance(cloudScroll, cloudDriftSeconds / window);

            clouds.uvRect = new Rect(cloudScroll, 0f, window, 1f);
        }

        /// <summary>
        /// Sways the dust about its resting place. Two axes on lengths that do not divide into each
        /// other, so the motes wander instead of sliding back and forth on one line.
        /// </summary>
        private void DriftDust()
        {
            if (dust == null || stage == null)
                return;

            dustPhase = new Vector2(
                Advance(dustPhase.x, dustDriftSeconds.x),
                Advance(dustPhase.y, dustDriftSeconds.y));

            // Against the stage rather than the screen, so the sway keeps its size in the picture
            // whatever is cropped off the edges.
            var size = stage.rect.size;

            dust.rectTransform.anchoredPosition = new Vector2(
                dustDrift.x * size.x * Mathf.Sin(dustPhase.x * Mathf.PI * 2f),
                dustDrift.y * size.y * Mathf.Sin(dustPhase.y * Mathf.PI * 2f));
        }

        /// <summary>
        /// Fades the light rays up and down. The pulse only ever takes light away from what was
        /// painted: alpha stops at 1, so pushing past it would hold the rays flat at full for part
        /// of every cycle instead of brightening them.
        /// </summary>
        private void PulseLight()
        {
            if (lightRays == null)
                return;

            lightPhase = Advance(lightPhase, lightPulseSeconds);

            // Starts the cycle at full strength, so the screen comes up on the artwork as painted.
            var color = lightRays.color;
            color.a = 1f - lightPulse * 0.5f * (1f - Mathf.Cos(lightPhase * Mathf.PI * 2f));
            lightRays.color = color;
        }

        /// <summary>
        /// Moves a 0..1 phase on by one frame and wraps it, so a kiosk day of drifting cannot run
        /// any of these out of float precision. A length of zero parks the phase where it is, which
        /// is what holds a layer still.
        /// </summary>
        private static float Advance(float phase, float seconds)
        {
            return seconds == 0f
                ? phase
                : Mathf.Repeat(phase + Time.unscaledDeltaTime / seconds, 1f);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            // Never leave GameResetter thinking a start image it can no longer see is still up.
            IsShowing = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == firstSceneName)
                Show();
        }

        [ContextMenu("Show")]
        public void Show()
        {
            // No image to show, or it is already up (Awake and the first sceneLoaded both call this).
            if (overlay == null || IsShowing)
                return;

            IsShowing = true;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            overlay.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // The aspect fitter only sizes the stage in the canvas rebuild, which runs after Update
            // — without this the clouds would be measured against an unfitted stage and show up at
            // the wrong size for the first frame the screen is up.
            if (stage != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(stage);

            // Nothing may move behind the image while the visitor has not started yet.
            PlayerController.EnableMovement(false);

            if (armRoutine != null)
                StopCoroutine(armRoutine);

            armRoutine = StartCoroutine(ArmButtonsAfterDelay());

            if (debugLogs)
                Debug.Log("StartSplash: start image is up, waiting for a language");

            OnShown?.Invoke();
        }

        [ContextMenu("Hide")]
        public void HideWithFade()
        {
            Hide(instant: false);
        }

        public void Hide(bool instant)
        {
            if (!IsShowing)
                return;

            IsShowing = false;

            OnHidden?.Invoke();

            SetButtonsInteractable(false);

            if (armRoutine != null)
            {
                StopCoroutine(armRoutine);
                armRoutine = null;
            }

            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);

            fadeRoutine = StartCoroutine(FadeOutRoutine(instant ? 0f : fadeOutDuration));
        }

        private IEnumerator ArmButtonsAfterDelay()
        {
            SetButtonsInteractable(false);

            if (inputDelay > 0f)
                yield return new WaitForSecondsRealtime(inputDelay);

            SetButtonsInteractable(true);

            armRoutine = null;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var button in languageButtons)
            {
                if (button != null)
                    button.interactable = interactable;
            }
        }

        private void SelectLanguage(Language language)
        {
            // The fade has already started: the visitor got their language, a second press must not
            // change it out from under the play-through that is starting.
            if (!IsShowing)
                return;

            // Assigning is what builds the dialog paragraphs for this run, so it happens on every
            // start — also when the language matches what the previous visitor picked.
            Node.CurrentLanguage = language;

            if (debugLogs)
                Debug.Log($"StartSplash: starting the game in {language}");

            Hide(instant: false);
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            var elapsed = 0f;

            // blocksRaycasts stays on for the whole fade, so the press that starts the game cannot
            // also land on something in the scene behind the image.
            while (elapsed < duration)
            {
                canvasGroup.alpha = 1f - elapsed / duration;
                elapsed += Time.unscaledDeltaTime;

                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            overlay.SetActive(false);
            fadeRoutine = null;

            PlayerController.EnableMovement(true);

            if (debugLogs)
                Debug.Log("StartSplash: start image is gone, scene is playable");
        }

        private void BuildOverlay()
        {
            // Nothing but the layered artwork is expected here; the single image is what the screen
            // ran on before the layers existed and is kept as the way to walk the language flow
            // through without any art at all.
            var hasLayers = skyLayer != null || cloudLayer != null || dustLayer != null
                            || lightLayer != null || FirstForegroundLayer() != null;
            var singleImage = hasLayers ? null : ResolveSprite();

            if (!hasLayers && singleImage == null)
            {
                Debug.LogWarning($"StartSplash: no artwork assigned and none found at Resources/{resourcePath} — the game starts without a start screen.", this);
                return;
            }

            overlay = new GameObject("StartSplashCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            overlay.transform.SetParent(transform, false);
            overlay.SetActive(false);

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            canvasGroup = overlay.GetComponent<CanvasGroup>();

            // The artwork rarely matches the screen aspect exactly, and none of the running scene may
            // show through where it does not reach.
            var background = CreateStretchedImage("Background", overlay.transform);
            background.color = backgroundColor;

            stage = CreateStage(hasLayers ? StageSprite() : singleImage);

            if (hasLayers)
                BuildLayers();
            else
                AddLayer("Image", stage).sprite = singleImage;

            // Added to the canvas rather than to the stage, so cropping the artwork on a narrow
            // screen cannot push them off the edge. Added last so they sit on top of it.
            BuildLanguageButtons(overlay.transform);
        }

        /// <summary>
        /// The rect the whole stack is drawn into. Fitting the stack once instead of every layer on
        /// its own is what keeps the layers registered with each other: each one fitted separately
        /// would land at its own scale on any screen that is not the aspect the artwork was painted
        /// at, and the clouds would drift off the horizon.
        /// </summary>
        private RectTransform CreateStage(Sprite reference)
        {
            var go = new GameObject("Stage", typeof(RectTransform));

            var rect = (RectTransform)go.transform;
            rect.SetParent(overlay.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (fit != ImageFit.Stretch)
            {
                // EnvelopeParent fills the screen and crops, FitInParent letterboxes onto the backdrop.
                var fitter = go.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = fit == ImageFit.Cover
                    ? AspectRatioFitter.AspectMode.EnvelopeParent
                    : AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = reference.rect.width / reference.rect.height;
            }

            return rect;
        }

        /// <summary>
        /// The layer the stage takes its shape from. The cloud strip is delivered cropped to the
        /// clouds and says nothing about the frame, so a full canvas layer is asked first.
        /// </summary>
        private Sprite StageSprite()
        {
            var foreground = FirstForegroundLayer();

            if (foreground != null)
                return foreground;

            return skyLayer != null ? skyLayer : cloudLayer;
        }

        private Sprite FirstForegroundLayer()
        {
            if (foregroundLayers == null)
                return null;

            foreach (var layer in foregroundLayers)
            {
                if (layer != null)
                    return layer;
            }

            return null;
        }

        private void BuildLayers()
        {
            if (skyLayer != null)
                AddLayer(skyLayer.name, stage).sprite = skyLayer;

            if (cloudLayer != null)
                clouds = AddCloudBand();

            if (foregroundLayers != null)
            {
                foreach (var layer in foregroundLayers)
                {
                    if (layer != null)
                        AddLayer(layer.name, stage).sprite = layer;
                }
            }

            if (dustLayer != null)
                dust = AddDustLayer();

            if (lightLayer != null)
            {
                lightRays = AddLayer(lightLayer.name, stage);
                lightRays.sprite = lightLayer;
            }
        }

        /// <summary>
        /// The swaying layer. Its anchors reach past the stage far enough to cover the drift, so at
        /// the far end of every sway the layer's own edge lands on the edge of the picture at worst
        /// and never inside it. Anchors rather than pixels, so that holds at any screen size, and
        /// the same fraction on both axes, so the layer is not stretched out of shape to get it.
        /// </summary>
        private Image AddDustLayer()
        {
            var margin = Mathf.Max(Mathf.Abs(dustDrift.x), Mathf.Abs(dustDrift.y)) * Vector2.one;

            var layer = AddLayer(dustLayer.name, stage);
            layer.sprite = dustLayer;

            var rect = layer.rectTransform;
            rect.anchorMin = -margin;
            rect.anchorMax = Vector2.one + margin;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return layer;
        }

        /// <summary>
        /// The scrolling strip. It is drawn through a RawImage rather than an Image because that is
        /// what lets the uv window be moved: the strip is set to repeat, so sliding the window is the
        /// whole loop — no seam to jump back over and no second copy to keep in step.
        /// </summary>
        private RawImage AddCloudBand()
        {
            var go = new GameObject("Clouds", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));

            // Anchored in fractions of the stage rather than in pixels, so the band keeps its place
            // in the picture at every screen size without anything being recomputed when it changes.
            var rect = (RectTransform)go.transform;
            rect.SetParent(stage, false);
            rect.anchorMin = new Vector2(0f, 1f - Mathf.Max(cloudBandTop, cloudBandBottom));
            rect.anchorMax = new Vector2(1f, 1f - Mathf.Min(cloudBandTop, cloudBandBottom));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var raw = go.GetComponent<RawImage>();
            raw.texture = cloudLayer.texture;
            raw.raycastTarget = false;

            return raw;
        }

        private static Image AddLayer(string name, Transform parent)
        {
            var layer = CreateStretchedImage(name, parent);

            // The buttons on top have to get the click, not the artwork.
            layer.raycastTarget = false;

            return layer;
        }

        private void BuildLanguageButtons(Transform parent)
        {
            languageButtons.Clear();

            // Offsets are in half-widths from the centre, so the pair stays centred whatever the
            // button size and spacing are set to.
            languageButtons.Add(BuildLanguageButton(parent, "GermanButton", germanButtonImage, germanLabel, -0.5f, Language.De));
            languageButtons.Add(BuildLanguageButton(parent, "EnglishButton", englishButtonImage, englishLabel, 0.5f, Language.En));

            SetButtonsInteractable(false);
        }

        private Button BuildLanguageButton(Transform parent, string name, Sprite sprite, string label, float side, Language language)
        {
            var framed = buttonFrame != null;
            var size = framed ? NativeSize(buttonFrameGlow != null ? buttonFrameGlow : buttonFrame) : buttonSize;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(side * (size.x + buttonSpacing), buttonBottomMargin);

            var image = go.GetComponent<Image>();
            var button = go.GetComponent<Button>();

            if (framed)
            {
                // The plate itself stays invisible and only catches the click — the look is the two
                // sprite layers below it.
                image.color = Color.clear;

                // Both layers sit at their own native size, centred. The glow file is the same frame
                // with the light painted around it, which is why it is the bigger of the two: giving
                // each its own rect is what keeps the frame from jumping when the glow comes in.
                AddSpriteLayer(rect, "Frame", buttonFrame);
                var glow = AddSpriteLayer(rect, "Glow", buttonFrameGlow != null ? buttonFrameGlow : buttonFrame);

                // Only the glow layer is tinted: invisible at rest, opaque under the pointer. The
                // frame underneath is a separate graphic, so it never changes.
                button.targetGraphic = glow;
                button.transition = Selectable.Transition.ColorTint;
                button.colors = GlowOnHover();

                BuildButtonLabel(rect, label);
            }
            else if (sprite != null)
            {
                image.sprite = sprite;
                button.targetGraphic = image;
            }
            else
            {
                // No artwork yet: a plain plate with the language written on it, so the screen can
                // be walked through before the flags are delivered.
                image.color = buttonColor;
                BuildButtonLabel(rect, label);
                button.targetGraphic = image;
            }

            button.onClick.AddListener(() => SelectLanguage(language));

            return button;
        }

        private static Vector2 NativeSize(Sprite sprite)
        {
            return new Vector2(sprite.rect.width, sprite.rect.height);
        }

        private static Image AddSpriteLayer(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = NativeSize(sprite);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;

            // The plate above has to get the click, not the artwork on top of it.
            image.raycastTarget = false;

            return image;
        }

        /// <summary>
        /// Tint block for the glow layer: transparent in every state except while the pointer is on
        /// the button. Disabled is transparent too, so nothing lights up during the input delay.
        /// </summary>
        private static ColorBlock GlowOnHover()
        {
            var hidden = new Color(1f, 1f, 1f, 0f);

            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = hidden;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = hidden;
            colors.disabledColor = hidden;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;

            return colors;
        }

        private void BuildButtonLabel(Transform parent, string label)
        {
            var go = new GameObject("Label", typeof(RectTransform));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.color = buttonTextColor;
            text.alignment = TextAlignmentOptions.Center;

            if (labelFont != null)
            {
                // Fixed size rather than auto sizing, so both buttons read at exactly the size the
                // main menu uses instead of each shrinking to its own label.
                text.font = labelFont;
                text.fontSize = labelFontSize;
                text.enableAutoSizing = false;
            }
            else
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 64f;
            }

            // The button underneath has to get the click, not the label on top of it.
            text.raycastTarget = false;
        }

        private static Image CreateStretchedImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return go.GetComponent<Image>();
        }

        private Sprite ResolveSprite()
        {
            if (!string.IsNullOrEmpty(resourcePath))
            {
                var loaded = Resources.Load<Sprite>(resourcePath);
                if (loaded != null)
                    return loaded;

                // Artwork dropped in with the default import settings is still a plain texture.
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                    return ToSprite(texture);
            }

            return usePlaceholderWhenMissing ? CreatePlaceholder() : null;
        }

        private Sprite CreatePlaceholder()
        {
            Debug.LogWarning($"StartSplash: showing a generated placeholder. Assign the start image in the inspector or drop it into Assets/Resources/{resourcePath}.", this);

            const int width = 320;
            const int height = 180;
            const int stripeWidth = 20;

            var dark = new Color(0.09f, 0.09f, 0.11f);
            var light = new Color(0.17f, 0.10f, 0.21f);

            var texture = new Texture2D(width, height) { name = "StartSplashPlaceholder", filterMode = FilterMode.Point };
            var pixels = new Color[width * height];

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = (x + y) / stripeWidth % 2 == 0 ? dark : light;

            texture.SetPixels(pixels);
            texture.Apply();

            return ToSprite(texture);
        }

        private static Sprite ToSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
