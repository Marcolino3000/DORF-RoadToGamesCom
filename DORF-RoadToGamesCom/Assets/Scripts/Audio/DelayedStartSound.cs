using System.Collections;
using ScenesSwitches;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Spielt einen Sound eine feste Zeit nachdem der Startscreen verschwunden ist.
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

        [SerializeField] private bool debugLogs;

        private Coroutine countdown;
        private bool alreadyPlayed;

        private void OnEnable()
        {
            StartSplash.OnHidden += HandleStartScreenHidden;
        }

        private void OnDisable()
        {
            StartSplash.OnHidden -= HandleStartScreenHidden;
        }

        private void Start()
        {
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

            if (!played)
                Debug.LogWarning($"{nameof(DelayedStartSound)} auf {name}: weder Wwise-Event noch AudioClip zugewiesen.", this);
            else if (debugLogs)
                Debug.Log($"{nameof(DelayedStartSound)}: Sound {delaySeconds}s nach dem Startscreen abgespielt.", this);
        }
    }
}
