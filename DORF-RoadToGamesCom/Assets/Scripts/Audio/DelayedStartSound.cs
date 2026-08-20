using System.Collections;
using ScenesSwitches;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Spielt einen Sound eine feste Zeit nachdem der Startscreen verschwunden ist, und blinkt dazu
    /// das Sprite im Child zweimal weg und wieder herein.
    ///
    /// Der Startscreen liegt über der bereits geladenen Szene 1, dieses Objekt existiert also
    /// schon, während der Visitor noch seine Sprache wählt. Gezählt wird ab
    /// <see cref="StartSplash.OnHidden"/> — das feuert, wenn das Bild anfängt auszublenden, nicht
    /// wenn es fertig ausgeblendet ist. Wer ab dem Ende des Ausblendens messen will, rechnet die
    /// Fade-Dauer des StartSplash auf <see cref="delaySeconds"/> drauf.
    ///
    /// Kiosk-Reset lädt Szene 1 neu, also wird das Objekt neu gebaut und der Timer läuft wieder von
    /// vorne. Läuft die Szene ohne Startscreen (Editor-Iteration, kein Startbild hinterlegt), holt
    /// <see cref="Start"/> das nach, damit der Sound nicht ganz ausfällt.
    /// </summary>
    public class DelayedStartSound : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Sekunden zwischen dem Verschwinden des Startscreens und dem Sound.")]
        [SerializeField] private float delaySeconds = 4f;

        [Header("Sound — Wwise-Event oder AudioClip, je nachdem was gesetzt ist")]
        [SerializeField] private AK.Wwise.Event wwiseEvent;
        [SerializeField] private AudioClip clip;
        [Tooltip("Optional. Bleibt das leer, wird die AudioSource auf diesem GameObject benutzt.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Blinken")]
        [Tooltip("Optional. Bleibt das leer, wird der erste SpriteRenderer in den Children benutzt.")]
        [SerializeField] private SpriteRenderer blinkSprite;
        [Tooltip("Wie oft das Sprite weg- und wieder hereinblendet.")]
        [SerializeField] private int blinkCount = 2;
        [Tooltip("Sekunden für das Ausblenden auf Alpha 0.")]
        [SerializeField] private float fadeOutDuration = 0.15f;
        [Tooltip("Sekunden für das Einblenden zurück auf den Ausgangswert.")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [Tooltip("Pause zwischen den beiden Blinks.")]
        [SerializeField] private float pauseBetweenBlinks = 0.1f;

        [SerializeField] private bool debugLogs;

        private Coroutine countdown;
        private Coroutine blink;
        private bool alreadyPlayed;

        // Das Sprite darf absichtlich halbtransparent sein — eingeblendet wird auf den Wert zurück,
        // der beim Start dran stand, nicht pauschal auf 1.
        private float baseAlpha = 1f;
        private bool baseAlphaKnown;

        private void OnEnable()
        {
            StartSplash.OnHidden += HandleStartScreenHidden;
        }

        private void OnDisable()
        {
            StartSplash.OnHidden -= HandleStartScreenHidden;

            // Ein Abbruch mitten im Blinken darf das Sprite nicht unsichtbar zurücklassen.
            if (blink != null)
            {
                StopCoroutine(blink);
                blink = null;
                SetAlpha(baseAlpha);
            }
        }

        private void Start()
        {
            CacheSprite();

            // Der StartSplash zeigt sich aus Awake heraus, also noch bevor Szenenobjekte laufen.
            // Steht er hier trotzdem nicht, gibt es keinen Startscreen, auf den zu warten wäre.
            if (!StartSplash.IsShowing)
                HandleStartScreenHidden();
        }

        private void HandleStartScreenHidden()
        {
            if (alreadyPlayed || countdown != null)
                return;

            countdown = StartCoroutine(PlayAfterDelay());
        }

        private IEnumerator PlayAfterDelay()
        {
            yield return new WaitForSeconds(delaySeconds);

            countdown = null;
            alreadyPlayed = true;

            Play();
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
                Debug.Log($"{nameof(DelayedStartSound)}: Sound {delaySeconds}s nach dem Startscreen abgespielt.", this);
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
