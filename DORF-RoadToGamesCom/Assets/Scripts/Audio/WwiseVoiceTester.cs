using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Spielt ein einzelnes Wwise-Voice-Event über das Kontextmenü der Komponente ab:
    /// Rechtsklick auf den Header im Inspector, dann "Play Voice". Damit lassen sich
    /// Sprachaufnahmen abhören, ohne die Szene durchzuspielen, die sie normal auslöst.
    ///
    /// Funktioniert auch außerhalb des Play Mode — die Wwise-Integration hält die Sound
    /// Engine im Editor am Laufen und registriert die Scene-View-Kamera als Listener.
    /// Gepostet wird auf diesem GameObject, ein 3D-positioniertes Event klingt also von
    /// dort, wo dieses Objekt steht.
    /// </summary>
    /// <remarks>
    /// AkGameObj ist absichtlich Pflicht: Beim Posten auf ein GameObject ohne AkGameObj
    /// hängt Wwise die Komponente still selbst an (AkUnitySoundEngine.AutoRegister) — im
    /// Edit Mode würde ein Klick auf den Menüeintrag so nebenbei die Szene dirty machen.
    /// </remarks>
    [RequireComponent(typeof(AkGameObj))]
    public class WwiseVoiceTester : MonoBehaviour
    {
        [Header("Wwise Events")]
        [SerializeField] private AK.Wwise.Event voiceEvent;

        [Tooltip("Ausblendzeit von \"Stop Voice\", in Millisekunden.")]
        [SerializeField] private int stopFadeMs = 200;

        [ContextMenu("Play Voice")]
        public void PlayVoice()
        {
            if (voiceEvent == null || !voiceEvent.IsValid())
            {
                Debug.LogWarning($"[{nameof(WwiseVoiceTester)}] Kein Voice Event zugewiesen.", this);
                return;
            }

            // Post gibt kommentarlos AK_INVALID_PLAYING_ID zurück, solange die Engine unten
            // ist — im Build der Normalzustand, bis der AkInitializer des Bootstrappers läuft.
            if (!AkUnitySoundEngine.IsInitialized())
            {
                Debug.LogWarning(
                    $"[{nameof(WwiseVoiceTester)}] Wwise läuft nicht, '{voiceEvent.Name}' wurde nicht gepostet.", this);
                return;
            }

            var playingId = voiceEvent.Post(gameObject);
            if (playingId == AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
            {
                Debug.LogWarning(
                    $"[{nameof(WwiseVoiceTester)}] '{voiceEvent.Name}' startet nicht — SoundBank geladen?", this);
                return;
            }

            Debug.Log($"[{nameof(WwiseVoiceTester)}] Spielt '{voiceEvent.Name}' (Playing ID {playingId}).", this);
        }

        /// <summary>
        /// Stoppt alle Instanzen des Events auf diesem GameObject — Sprachaufnahmen sind lang
        /// genug, dass man sie beim Abhören abbrechen will.
        /// </summary>
        [ContextMenu("Stop Voice")]
        public void StopVoice()
        {
            if (voiceEvent == null || !voiceEvent.IsValid() || !AkUnitySoundEngine.IsInitialized())
                return;

            voiceEvent.Stop(gameObject, stopFadeMs);
        }
    }
}
