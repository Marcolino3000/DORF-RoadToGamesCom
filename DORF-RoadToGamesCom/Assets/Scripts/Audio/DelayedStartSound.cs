using System.Collections;
using Runtime.Scripts.Interactables;
using ScenesSwitches;
using UI;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Erinnert den Visitor an sein Handy: eine feste Zeit nachdem der Startscreen verschwunden
    /// ist, spielt ein Sound und das Sprite im Child blinkt zweimal weg und wieder herein. Das
    /// wiederholt sich im festen Takt, bis das Handy zum ersten Mal geöffnet wird — danach nie
    /// wieder, der Hinweis hat ja sein Ziel erreicht.
    ///
    /// Der Startscreen liegt über der bereits geladenen Szene 1, dieses Objekt existiert also
    /// schon, während der Visitor noch seine Sprache wählt. Gezählt wird ab
    /// <see cref="StartSplash.OnHidden"/> — das feuert, wenn das Bild anfängt auszublenden, nicht
    /// wenn es fertig ausgeblendet ist. Wer ab dem Ende des Ausblendens messen will, rechnet die
    /// Fade-Dauer des StartSplash auf <see cref="delaySeconds"/> drauf.
    ///
    /// Kiosk-Reset lädt Szene 1 neu, also wird das Objekt samt "Handy war offen"-Merker neu
    /// gebaut und der Hinweis läuft für den nächsten Visitor wieder an. <see cref="OnSceneSetup"/>
    /// setzt den Merker zusätzlich explizit zurück, damit das auch dann stimmt, wenn der Reset
    /// später einmal ohne Szenenwechsel auskommt. Läuft die Szene ohne Startscreen
    /// (Editor-Iteration, kein Startbild hinterlegt), holt <see cref="Start"/> den Anstoß nach.
    /// </summary>
    public class DelayedStartSound : MonoBehaviour, ISceneSetupCallbackReceiver
    {
        [Header("Timing")]
        [Tooltip("Sekunden zwischen dem Verschwinden des Startscreens und dem ersten Hinweis.")]
        [SerializeField] private float delaySeconds = 4f;
        [Tooltip("Sekunden zwischen zwei Hinweisen, solange das Handy nicht geöffnet wurde.")]
        [SerializeField] private float repeatIntervalSeconds = 10f;

        [Header("Sound — Wwise-Event oder AudioClip, je nachdem was gesetzt ist")]
        [SerializeField] private AK.Wwise.Event wwiseEvent;
        [SerializeField] private AudioClip clip;
        [Tooltip("Optional. Bleibt das leer, wird die AudioSource auf diesem GameObject benutzt.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Blinken")]
        [Tooltip("Optional. Bleibt das leer, wird der erste SpriteRenderer in den Children benutzt.")]
        [SerializeField] private SpriteRenderer blinkSprite;
        [Tooltip("Wie oft das Sprite pro Hinweis weg- und wieder hereinblendet.")]
        [SerializeField] private int blinkCount = 2;
        [Tooltip("Sekunden für das Ausblenden auf Alpha 0.")]
        [SerializeField] private float fadeOutDuration = 0.15f;
        [Tooltip("Sekunden für das Einblenden zurück auf den Ausgangswert.")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [Tooltip("Pause zwischen den beiden Blinks.")]
        [SerializeField] private float pauseBetweenBlinks = 0.1f;

        [SerializeField] private bool debugLogs;

        private Coroutine reminders;
        private Coroutine blink;

        /// <summary>
        /// Einmal true, bleibt es true: der Hinweis ist angekommen, auch wenn das Handy wieder zu
        /// ist. Instanzfeld und kein static, damit der nächste Visitor bei null anfängt.
        /// </summary>
        private bool smartphoneWasOpened;

        // Das Sprite darf absichtlich halbtransparent sein — eingeblendet wird auf den Wert zurück,
        // der beim Start dran stand, nicht pauschal auf 1.
        private float baseAlpha = 1f;
        private bool baseAlphaKnown;

        private void OnEnable()
        {
            StartSplash.OnHidden += HandleStartScreenHidden;
            Smartphone.OnOpenStateChanged += HandleSmartphoneOpenStateChanged;
        }

        private void OnDisable()
        {
            StartSplash.OnHidden -= HandleStartScreenHidden;
            Smartphone.OnOpenStateChanged -= HandleSmartphoneOpenStateChanged;

            StopReminders();

            // Ein Abbruch mitten im Blinken darf das Sprite nicht unsichtbar zurücklassen.
            StopBlinking();
        }

        private void Start()
        {
            CacheSprite();

            // Der StartSplash zeigt sich aus Awake heraus, also noch bevor Szenenobjekte laufen.
            // Steht er hier trotzdem nicht, gibt es keinen Startscreen, auf den zu warten wäre.
            if (!StartSplash.IsShowing)
                HandleStartScreenHidden();
        }

        /// <summary>
        /// Läuft bei jedem Szenenload und beim Inaktivitäts-Reset; SceneSetup findet das Objekt
        /// über FindObjectsByType, es braucht also keine Verdrahtung. Fasst die laufenden Hinweise
        /// nicht an — beim Szenenload ist hier noch keiner gestartet (Start läuft danach), und
        /// beim Reset wird die Szene ohnehin neu gebaut.
        /// </summary>
        public void OnSceneSetup()
        {
            smartphoneWasOpened = false;
            StopBlinking();
        }

        private void HandleStartScreenHidden()
        {
            if (reminders != null || smartphoneWasOpened)
                return;

            reminders = StartCoroutine(RemindUntilPhoneOpened());
        }

        private void HandleSmartphoneOpenStateChanged(bool open)
        {
            if (!open || smartphoneWasOpened)
                return;

            smartphoneWasOpened = true;
            StopReminders();

            if (debugLogs)
                Debug.Log($"{nameof(DelayedStartSound)}: Handy geöffnet, keine weiteren Hinweise.", this);
        }

        private IEnumerator RemindUntilPhoneOpened()
        {
            yield return new WaitForSeconds(delaySeconds);

            // Der Takt zählt ab dem Abspielen, nicht ab dem Ende des Blinkens — bei einem Blink von
            // unter einer Sekunde ist der Unterschied nicht zu sehen, aber der Abstand bleibt fest.
            while (!smartphoneWasOpened)
            {
                Play();

                yield return new WaitForSeconds(repeatIntervalSeconds);
            }

            reminders = null;
        }

        [ContextMenu("Play")]
        private void Play()
        {
            var played = false;

            if (wwiseEvent != null && wwiseEvent.IsValid())
            {
                wwiseEvent.Post(gameObject);
                played = true;
            }

            if (clip != null)
            {
                if (audioSource == null)
                    audioSource = GetComponent<AudioSource>();

                if (audioSource != null)
                {
                    audioSource.PlayOneShot(clip);
                    played = true;
                }
                else
                {
                    Debug.LogWarning($"{nameof(DelayedStartSound)} auf {name}: AudioClip gesetzt, aber keine AudioSource.", this);
                }
            }

            StartBlinking();

            if (!played)
                Debug.LogWarning($"{nameof(DelayedStartSound)} auf {name}: weder Wwise-Event noch AudioClip zugewiesen.", this);
            else if (debugLogs)
                Debug.Log($"{nameof(DelayedStartSound)}: Hinweis abgespielt.", this);
        }

        private void StopReminders()
        {
            if (reminders == null)
                return;

            StopCoroutine(reminders);
            reminders = null;
        }

        private void StartBlinking()
        {
            CacheSprite();

            if (blinkSprite == null)
            {
                Debug.LogWarning($"{nameof(DelayedStartSound)} auf {name}: kein SpriteRenderer im Child, kein Blinken.", this);
                return;
            }

            if (blink != null)
                StopCoroutine(blink);

            blink = StartCoroutine(BlinkRoutine());
        }

        private void StopBlinking()
        {
            if (blink != null)
            {
                StopCoroutine(blink);
                blink = null;
            }

            if (baseAlphaKnown)
                SetAlpha(baseAlpha);
        }

        private IEnumerator BlinkRoutine()
        {
            for (var i = 0; i < blinkCount; i++)
            {
                yield return FadeAlpha(baseAlpha, 0f, fadeOutDuration);
                yield return FadeAlpha(0f, baseAlpha, fadeInDuration);

                if (i < blinkCount - 1 && pauseBetweenBlinks > 0f)
                    yield return new WaitForSeconds(pauseBetweenBlinks);
            }

            SetAlpha(baseAlpha);
            blink = null;
        }

        private IEnumerator FadeAlpha(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
                elapsed += Time.deltaTime;

                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (blinkSprite == null)
                return;

            var color = blinkSprite.color;
            color.a = alpha;
            blinkSprite.color = color;
        }

        private void CacheSprite()
        {
            if (blinkSprite == null)
                blinkSprite = GetComponentInChildren<SpriteRenderer>(true);

            if (blinkSprite == null || baseAlphaKnown)
                return;

            baseAlpha = blinkSprite.color.a;
            baseAlphaKnown = true;
        }
    }
}
