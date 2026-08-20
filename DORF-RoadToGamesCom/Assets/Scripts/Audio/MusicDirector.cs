using System;
using System.Collections;
using System.Collections.Generic;
using ScenesSwitches;
using Tree;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Audio
{
    /// <summary>
    /// Die eine Stelle, die Wwise sagt, welche Musik laufen soll. Sitzt auf dem Global-Prefab und
    /// überlebt damit jeden Szenenwechsel und den Kiosk-Reset. Sie hört zu, statt gerufen zu werden:
    /// kein anderes Skript muss wissen, dass es Musik gibt.
    ///
    /// Sechs Momente sind verdrahtet — Startbildschirm erscheint, Scene 1 startet, irgendeine andere
    /// Szene startet, ein Dialog beginnt, das Smartphone wird geöffnet, ein Menü geht auf. Jeder ist ein
    /// <see cref="MusicCue"/>, der ein Wwise-Event posten, einen Wwise-State setzen (im
    /// Wwise-Projekt ist das die Gruppe ST_MX_Context) oder beides tun kann. Ein leerer Slot tut
    /// nichts — so lässt sich genau so viel verdrahten, wie das Wwise-Projekt gerade hergibt.
    ///
    /// Zwei Reihenfolgen sind hier absichtlich so gelöst:
    /// Der Startbildschirm gewinnt gegen die Szenen-Cue. StartSplash.Show() läuft in Awake und noch
    /// einmal beim ersten sceneLoaded, die Szene lädt also, während das Bild schon steht. Die
    /// Szenen-Cue wartet deshalb einen Frame und wird zurückgehalten, solange
    /// <see cref="StartSplash.IsShowing"/> gilt — sie kommt erst, wenn eine Sprache gewählt wurde.
    /// Und <see cref="DialogTreeRunner.OnDialogRunningStatusChanged"/> feuert bei jedem Knoten mit
    /// true, nicht nur zum Dialogbeginn, wird hier also auf die Flanke geprüft.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        /// <summary>
        /// Was in Wwise passieren soll. Beide Felder sind optional: leer heißt "nichts tun".
        /// Der State wird vor dem Event gesetzt, damit das Event schon im richtigen Kontext startet.
        /// </summary>
        [Serializable]
        public class MusicCue
        {
            [Tooltip("Wwise-Event, das auf diesem GameObject gepostet wird. Leer lassen, wenn nur der State wechseln soll.")]
            public AK.Wwise.Event wwiseEvent;

            [Tooltip("Wwise-State (z.B. aus ST_MX_Context). Wird vor dem Event gesetzt.")]
            public AK.Wwise.State state;

            public bool HasEvent => wwiseEvent != null && wwiseEvent.IsValid();
            public bool HasState => state != null && state.IsValid();
            public bool IsEmpty => !HasEvent && !HasState;
        }

        /// <summary>Eine Cue für eine bestimmte Szene, über den Szenennamen zugeordnet.</summary>
        [Serializable]
        public class SceneMusicCue
        {
            [Tooltip("Name wie in den Build Settings, z.B. \"Scene 1\".")]
            public string sceneName;

            public MusicCue cue;
        }

        [Header("Startbildschirm")]
        [Tooltip("Sobald das Startbild steht — beim Start und nach jedem Kiosk-Reset.")]
        [SerializeField] private MusicCue startScreen;

        [Header("Szenen")]
        [Tooltip("Pro Szene eine Cue. Nicht gelistete Szenen bekommen die Cue darunter.")]
        [SerializeField] private List<SceneMusicCue> scenes = new();

        [Tooltip("Für jede Szene, die oben nicht steht. Leer lassen, wenn nur gelistete Szenen Musik wechseln sollen.")]
        [SerializeField] private MusicCue anyOtherScene;

        [Header("Dialog")]
        [Tooltip("Wenn ein Dialogbaum anfängt zu laufen.")]
        [SerializeField] private MusicCue dialogStarted;

        [Tooltip("Optional: wenn der Dialog vorbei ist. Leer lassen, damit die Dialogmusik weiterläuft.")]
        [SerializeField] private MusicCue dialogEnded;

        [Header("Smartphone")]
        [Tooltip("Wenn das Handy aufgeklappt wird.")]
        [SerializeField] private MusicCue smartphoneOpened;

        [Tooltip("Optional: wenn das Handy wieder zugeht.")]
        [SerializeField] private MusicCue smartphoneClosed;

        [Header("Menü")]
        [Tooltip("Wenn ein Menü aufgeht — Hauptmenü, Journal, Karte oder Einstellungen. Umschalten " +
                 "zwischen zwei Menüs zählt nicht als neues Aufgehen.")]
        [SerializeField] private MusicCue menuOpened;

        [Tooltip("Optional: wenn das letzte Menü wieder zugeht. Leer lassen, damit die Menümusik weiterläuft.")]
        [SerializeField] private MusicCue menuClosed;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private bool startScreenCuePlayed;
        private bool dialogWasRunning;
        private string pendingSceneName;
        private Coroutine sceneCueRoutine;

        // Eine Cue, die anlag, bevor die Sound Engine oben war. Nur die letzte zählt: bei Musik ist
        // der zuletzt gewünschte Zustand immer der richtige.
        private MusicCue queuedCue;
        private string queuedReason;
        private Coroutine engineWaitRoutine;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            StartSplash.OnShown += HandleStartScreenShown;
            StartSplash.OnHidden += HandleStartScreenHidden;
            DialogTreeRunner.OnDialogRunningStatusChanged += HandleDialogRunningChanged;
            Smartphone.OnOpenStateChanged += HandleSmartphoneOpenChanged;
            MenuToggle.OnMenuOpenStateChanged += HandleMenuOpenChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StartSplash.OnShown -= HandleStartScreenShown;
            StartSplash.OnHidden -= HandleStartScreenHidden;
            DialogTreeRunner.OnDialogRunningStatusChanged -= HandleDialogRunningChanged;
            Smartphone.OnOpenStateChanged -= HandleSmartphoneOpenChanged;
            MenuToggle.OnMenuOpenStateChanged -= HandleMenuOpenChanged;
        }

        private void Start()
        {
            // StartSplash zeigt sich in Awake. Ob das vor oder nach dem OnEnable hier lief, hängt an
            // der Komponentenreihenfolge im Prefab — Start läuft nach allen Awakes, hier lässt sich
            // ein verpasstes OnShown also nachholen.
            if (StartSplash.IsShowing)
                HandleStartScreenShown();
        }

        private void HandleStartScreenShown()
        {
            if (startScreenCuePlayed)
                return;

            startScreenCuePlayed = true;
            Apply(startScreen, "Startbildschirm");
        }

        private void HandleStartScreenHidden()
        {
            startScreenCuePlayed = false;

            // Die Szene lief schon, während das Startbild darüber stand: jetzt ist sie zu sehen.
            if (pendingSceneName == null)
                return;

            var sceneName = pendingSceneName;
            pendingSceneName = null;
            PlaySceneCue(sceneName);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Der DialogTreeRunner der alten Szene ist weg, das statische Event bleibt. Wird beim
            // Kiosk-Reset mitten im Dialog neu geladen, kam nie ein false — ohne diese Zeile bliebe
            // die Flanke gesetzt und der nächste Dialog löste keine Cue mehr aus.
            dialogWasRunning = false;

            pendingSceneName = scene.name;

            if (sceneCueRoutine != null)
                StopCoroutine(sceneCueRoutine);

            sceneCueRoutine = StartCoroutine(PlaySceneCueNextFrame());
        }

        /// <summary>
        /// Einen Frame warten, bevor die Szenen-Cue läuft. StartSplash hängt am selben sceneLoaded;
        /// wer von beiden zuerst dran ist, entscheidet die Komponentenreihenfolge im Prefab. Nach
        /// einem Frame steht <see cref="StartSplash.IsShowing"/> fest, egal wie herum es lief.
        /// </summary>
        private IEnumerator PlaySceneCueNextFrame()
        {
            yield return null;

            sceneCueRoutine = null;

            // Startbild steht: die Cue wartet, bis eine Sprache gewählt wurde.
            if (StartSplash.IsShowing)
                yield break;

            var sceneName = pendingSceneName;
            pendingSceneName = null;
            PlaySceneCue(sceneName);
        }

        private void PlaySceneCue(string sceneName)
        {
            foreach (var entry in scenes)
            {
                if (entry == null || entry.sceneName != sceneName)
                    continue;

                Apply(entry.cue, $"Szene '{sceneName}'");
                return;
            }

            Apply(anyOtherScene, $"Szene '{sceneName}' (nicht gelistet)");
        }

        /// <summary>
        /// Läuft bei jedem Dialogknoten mit true, nicht nur beim ersten — die Cue darf also nur auf
        /// den Wechsel hin feuern, sonst startet die Musik bei jeder Zeile neu.
        /// </summary>
        private void HandleDialogRunningChanged(bool isRunning, DialogTree tree)
        {
            if (isRunning == dialogWasRunning)
                return;

            dialogWasRunning = isRunning;
            Apply(isRunning ? dialogStarted : dialogEnded, isRunning ? "Dialog startet" : "Dialog vorbei");
        }

        private void HandleSmartphoneOpenChanged(bool isOpen)
        {
            Apply(isOpen ? smartphoneOpened : smartphoneClosed, isOpen ? "Smartphone auf" : "Smartphone zu");
        }

        /// <summary>
        /// MenuToggle feuert nur auf den echten Wechsel, ein Sprung vom Journal in die Karte kommt
        /// hier also nicht an — die Menümusik läuft durch. Beim Kiosk-Reset räumt MenuToggle die
        /// offenen Menüs weg, während das Startbild hochkommt: das "zu" käme dann als Letztes und
        /// überschriebe die Startbild-Cue, darum gewinnt das Startbild wie überall sonst auch.
        /// </summary>
        private void HandleMenuOpenChanged(bool isOpen)
        {
            if (StartSplash.IsShowing)
                return;

            Apply(isOpen ? menuOpened : menuClosed, isOpen ? "Menü auf" : "Menü zu");
        }

        private void Apply(MusicCue cue, string reason)
        {
            if (cue == null || cue.IsEmpty)
                return;

            // Der Bootstrapper startet die Sound Engine, bevor dieses Prefab entsteht, das sollte
            // also nie greifen. Ein Post gegen eine tote Engine ist aber endgültig verloren (siehe
            // WwiseListenerRegistrar), darum wartet die Cue lieber.
            if (!AkUnitySoundEngine.IsInitialized())
            {
                queuedCue = cue;
                queuedReason = reason;

                if (engineWaitRoutine == null)
                    engineWaitRoutine = StartCoroutine(ApplyWhenEngineIsUp());

                return;
            }

            if (cue.HasState)
                cue.state.SetValue();

            if (cue.HasEvent)
                cue.wwiseEvent.Post(gameObject);

            if (debugLogs)
                Debug.Log($"MusicDirector: {reason} → State '{(cue.HasState ? cue.state.Name : "-")}', " +
                          $"Event '{(cue.HasEvent ? cue.wwiseEvent.Name : "-")}'");
        }

        private IEnumerator ApplyWhenEngineIsUp()
        {
            while (!AkUnitySoundEngine.IsInitialized())
                yield return null;

            engineWaitRoutine = null;

            var cue = queuedCue;
            var reason = queuedReason;
            queuedCue = null;
            queuedReason = null;

            Apply(cue, reason);
        }
    }
}
