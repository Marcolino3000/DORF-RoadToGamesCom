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
    /// The canvas is built in code: only the image itself has to be delivered, either dropped onto
    /// the Image field or into Assets/Resources under <see cref="resourcePath"/>.
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
        [Tooltip("Leave empty to load the image from Resources instead — see Resource Path.")]
        [SerializeField] private Sprite image;
        [Tooltip("Used when no image is assigned above: drop the artwork into Assets/Resources/ under this path. The file extension does not matter.")]
        [SerializeField] private string resourcePath = "UI/StartSplash";
        [Tooltip("Until the artwork is delivered, show a generated placeholder so the flow can be tested. Off: no image found means no start screen at all.")]
        [SerializeField] private bool usePlaceholderWhenMissing = true;

        [Header("Language")]
        [SerializeField] private Language startingLanguage = Language.De;
        [Tooltip("Leave empty to get a plain plate with the label written on it instead.")]
        [SerializeField] private Sprite germanButtonImage;
        [SerializeField] private Sprite englishButtonImage;
        [SerializeField] private string germanLabel = "Deutsch";
        [SerializeField] private string englishLabel = "English";
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

        private GameObject overlay;
        private CanvasGroup canvasGroup;
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

            // Nothing may move behind the image while the visitor has not started yet.
            PlayerController.EnableMovement(false);

            if (armRoutine != null)
                StopCoroutine(armRoutine);

            armRoutine = StartCoroutine(ArmButtonsAfterDelay());

            if (debugLogs)
                Debug.Log("StartSplash: start image is up, waiting for a language");
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
            var sprite = ResolveSprite();

            if (sprite == null)
            {
                Debug.LogWarning($"StartSplash: no image assigned and none found at Resources/{resourcePath} — the game starts without a start screen.", this);
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

            var splash = CreateStretchedImage("Image", overlay.transform);
            splash.sprite = sprite;

            if (fit != ImageFit.Stretch)
            {
                // EnvelopeParent fills the screen and crops, FitInParent letterboxes onto the backdrop.
                var fitter = splash.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = fit == ImageFit.Cover
                    ? AspectRatioFitter.AspectMode.EnvelopeParent
                    : AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            // Added last so they sit on top of the artwork.
            BuildLanguageButtons(overlay.transform);
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
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = buttonSize;
            rect.anchoredPosition = new Vector2(side * (buttonSize.x + buttonSpacing), buttonBottomMargin);

            var image = go.GetComponent<Image>();

            if (sprite != null)
            {
                image.sprite = sprite;
            }
            else
            {
                // No artwork yet: a plain plate with the language written on it, so the screen can
                // be walked through before the flags are delivered.
                image.color = buttonColor;
                BuildButtonLabel(rect, label);
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectLanguage(language));

            return button;
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
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 64f;

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
            if (image != null)
                return image;

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
