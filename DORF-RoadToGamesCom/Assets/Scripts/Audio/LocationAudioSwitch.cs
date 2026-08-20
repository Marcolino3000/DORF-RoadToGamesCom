using System.Collections;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Sagt Wwise, ob Marlene im Haus oder draußen ist. Im Wwise-Projekt ist das die State-Gruppe
    /// SCENE02_location mit "indoor" und "outdoor"; daran hängt der SwitchContainer "Scene02"
    /// (SwitchBehavior = continuous), der zwischen Raumton und Vogelgezwitscher überblendet.
    /// Deshalb ist der State hier das Werkzeug und nicht ein Event pro Wechsel: das Event startet
    /// die Ambience einmal, der State entscheidet, was davon zu hören ist. Solange der State auf
    /// "None" steht — der Default im Wwise-Projekt — kommt gar nichts.
    ///
    /// Bewusst ohne eigenen Collider: die Innen/Außen-Entscheidung fällt in Scene 2 schon. Die vier
    /// RoomTrigger unter "Trigger Location Switch" schalten über <see cref="RoomManager"/> die
    /// sichtbaren Räume um, und der meldet jeden Wechsel als <see cref="RoomManager.OnRoomChanged"/>.
    /// Ein zweiter Collider hier wäre eine zweite Wahrheit — sobald jemand einen der beiden
    /// verschiebt, zeigt das Bild den Garten und der Ton den Flur.
    ///
    /// Alle Slots sind optional, leer heißt "nichts tun".
    /// </summary>
    public class LocationAudioSwitch : MonoBehaviour
    {
        [Header("Wwise-State (SCENE02_location)")]
        [Tooltip("State für draußen, im Wwise-Projekt SCENE02_location > outdoor.")]
        [SerializeField] private AK.Wwise.State outdoorState;

        [Tooltip("State für drinnen, im Wwise-Projekt SCENE02_location > indoor.")]
        [SerializeField] private AK.Wwise.State indoorState;

        [Header("Ambience")]
        [Tooltip("Startet die Ambience einmal beim Szenenstart, nachdem der State steht — AMB_Scene2_Start. " +
                 "Ohne das bleibt der State stumm, weil niemand sonst die Ambience postet.")]
        [SerializeField] private AK.Wwise.Event ambienceStartEvent;

        [Tooltip("Wird beim Verlassen der Szene gepostet — AMB_Scene2_Stop. Ohne das läuft die " +
                 "Scene-2-Ambience nach dem Kiosk-Reset in Scene 1 weiter.")]
        [SerializeField] private AK.Wwise.Event ambienceStopEvent;

        [Header("Optionale Übergangs-Events")]
        [Tooltip("Einmaliges Event, wenn Marlene rausgeht. Beim Szenenstart feuert es nicht.")]
        [SerializeField] private AK.Wwise.Event enteredOutdoorEvent;

        [Tooltip("Einmaliges Event, wenn Marlene reingeht. Beim Szenenstart feuert es nicht.")]
        [SerializeField] private AK.Wwise.Event enteredIndoorEvent;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        // null heißt "noch nie gesetzt". Das erste Setzen bleibt ohne Übergangs-Event — der
        // Szenenstart ist kein Durch-die-Tür-Gehen.
        private bool? appliedIsOutside;

        // Ein Wechsel, der ankam, bevor die Sound Engine oben war. Nur der letzte zählt.
        private bool? pendingIsOutside;

        private bool engineReady;
        private bool ambienceRunning;

        private void OnEnable()
        {
            RoomManager.OnRoomChanged += HandleRoomChanged;
            StartCoroutine(SetUpWhenEngineIsUp());
        }

        private void OnDisable()
        {
            RoomManager.OnRoomChanged -= HandleRoomChanged;

            StopAmbience();

            engineReady = false;
            appliedIsOutside = null;
            pendingIsOutside = null;
        }

        private IEnumerator SetUpWhenEngineIsUp()
        {
            // Ein Frame Vorlauf. Erst danach sind alle Awake und Start durch, der RoomManager hat
            // seinen Startraum gemeldet und IsOutside stimmt auch beim zweiten Besuch in Scene 2.
            yield return null;

            // Der Bootstrapper fährt Wwise hoch, bevor irgendeine Szene lädt, das sollte also nie
            // greifen. Ein Post gegen eine tote Engine ist aber endgültig verloren, darum lieber
            // warten als eine Ausstellung lang stumm sein.
            while (!AkUnitySoundEngine.IsInitialized())
                yield return null;

            engineReady = true;

            ApplyLocation(pendingIsOutside ?? RoomManager.IsOutside);
            pendingIsOutside = null;

            StartAmbience();
        }

        private void HandleRoomChanged(bool isOutside)
        {
            if (!engineReady)
            {
                pendingIsOutside = isOutside;
                return;
            }

            ApplyLocation(isOutside);
        }

        private void ApplyLocation(bool isOutside)
        {
            var isFirst = appliedIsOutside == null;

            if (!isFirst && appliedIsOutside.Value == isOutside)
                return;

            appliedIsOutside = isOutside;

            var state = isOutside ? outdoorState : indoorState;
            var hasState = state != null && state.IsValid();

            if (hasState)
                state.SetValue();

            if (!isFirst)
                Post(isOutside ? enteredOutdoorEvent : enteredIndoorEvent);

            if (debugLogs)
                Debug.Log($"{nameof(LocationAudioSwitch)}: {(isOutside ? "draußen" : "drinnen")} → " +
                          $"State '{(hasState ? state.Name : "-")}'", this);
        }

        private void StartAmbience()
        {
            if (ambienceRunning)
                return;

            if (!Post(ambienceStartEvent))
                return;

            ambienceRunning = true;
        }

        private void StopAmbience()
        {
            if (!ambienceRunning)
                return;

            ambienceRunning = false;
            Post(ambienceStopEvent);
        }

        /// <summary>Postet, wenn es etwas zu posten gibt. Gibt zurück, ob wirklich gepostet wurde.</summary>
        private bool Post(AK.Wwise.Event wwiseEvent)
        {
            if (wwiseEvent == null || !wwiseEvent.IsValid())
                return false;

            if (!AkUnitySoundEngine.IsInitialized())
                return false;

            wwiseEvent.Post(gameObject);

            if (debugLogs)
                Debug.Log($"{nameof(LocationAudioSwitch)}: Event '{wwiseEvent.Name}' gepostet.", this);

            return true;
        }
    }
}
