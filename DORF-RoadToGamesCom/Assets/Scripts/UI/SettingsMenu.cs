using System;
using Audio;
using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class SettingsMenu : MonoBehaviour, ISceneSetupCallbackReceiver
    {
        public event Action OnResume;

        /// <summary>
        /// Tracked here rather than read back from the root element: after the UIDocument rebuilds
        /// its tree the cached root is a detached leftover, and the fresh one is visible because
        /// that is how the UXML authors it. See <see cref="Update"/>.
        /// </summary>
        public bool IsVisible { get; private set; }


        [Header("References")]
        [SerializeField] InGameAudioSettings audioSettings;
        [SerializeField] private InputDispatcher inputDispatcher;
        
        private Slider masterVolume;
        private Slider dialogVolume;
        private Slider musicVolume;
        private Slider sfxVolume;
        private Slider inactivityThreshold;
        private Toggle subtitles;
        private Button resumeButton;
        
        private UIDocument uiDocument;
        private VisualElement root;
        
        
        #region Helpers
        // private void ShowMenu()
        // {
        //     root.visible = true;
        //     // raycaster.isDialogRunning = true;
        // }
        // private void HideMenu()
        // {
        //     root.visible = false;
        //     // raycaster.isDialogRunning = false;
        // }
        
        #endregion
        
        #region Setup
        
        public void Setup()
        {
            GetElements();
            SetupEvents();
            SetSlidersToCurrentValues();
        }

        /// <summary>
        /// UIDocument throws away its visual tree and builds a new one whenever the source UXML or
        /// a USS it pulls in reimports during Play mode, and whenever the document is disabled and
        /// re-enabled. Every element cached in GetElements is detached at that point — which is why
        /// editing a stylesheet while playing used to leave this menu on screen with a Resume button
        /// wired to an element nobody can see. Detect it by the root swapping out, then re-acquire
        /// the elements, re-register their callbacks and restore the visibility we last set.
        /// </summary>
        private void Update()
        {
            // Re-fetched rather than trusted: uiDocument is not serialized, so a script recompile
            // in Play mode wipes it and Setup never runs again — the menu would be dead for the
            // session. A disabled UIDocument reports a null root, which must not count as changed.
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var current = uiDocument.rootVisualElement;
            if (current == null || current == root) return;

            var wasVisible = IsVisible;
            Setup();
            if (wasVisible) Show();
            else Hide();
        }

        private void GetElements()
        {
            uiDocument = GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
            
            masterVolume = root.Q<Slider>("masterVolume");
            dialogVolume = root.Q<Slider>("dialogVolume");
            musicVolume = root.Q<Slider>("musicVolume");
            sfxVolume = root.Q<Slider>("sfxVolume");
            inactivityThreshold = root.Q<Slider>("inactivityThreshold");
            subtitles = root.Q<Toggle>("subtitlesToggle");
            
            resumeButton = root.Q<Button>("resumeButton");
        }

        private void SetupEvents()
        {
            masterVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetMasterVolume(evt.newValue);
                });
            
            dialogVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetDialogVolume(evt.newValue);
                });
            
            musicVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetMusicVolume(evt.newValue);
                });
            
            sfxVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetSfxVolume(evt.newValue);
                });
            
            inactivityThreshold.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.inactivityThresholdSeconds = (int)evt.newValue;
                    inputDispatcher.secondsUntilGameReset = (int)evt.newValue;
                });
            
            // Null-guarded, unlike the sliders above: a UXML that has not reimported yet would
            // otherwise take the whole menu setup down with it and leave the kiosk without a
            // Resume button. Same reason the seeding below checks.
            if (subtitles != null)
                subtitles.RegisterValueChangedCallback(
                    evt =>
                    {
                        audioSettings.SetSubtitlesEnabled(evt.newValue);
                    });

            resumeButton.clicked += () => { OnResume?.Invoke(); };
        }
        
        /// <summary>
        /// Runs on every scene load and on the inactivity reset. The volumes are left alone on
        /// purpose — see InGameAudioSettings.OnSceneSetup; this used to restore them a second time,
        /// so dropping it there alone would not have been enough.
        ///
        /// The reset timeout is restored, and it also lives as a plain int on the InputDispatcher —
        /// which sits on the DontDestroyOnLoad Global prefab and is therefore never recreated. Read
        /// from the authored default rather than from the settings object, so it does not matter
        /// which of the two receivers ran first.
        /// </summary>
        public void OnSceneSetup()
        {
            if (audioSettings == null) return;

            // No-op after the first scene of the session. Called here as well so the sliders below
            // are seeded from the defaults even when this receiver runs before the settings object.
            audioSettings.ApplyLaunchDefaultsOnce();

            if (inputDispatcher != null)
                inputDispatcher.secondsUntilGameReset = audioSettings.DefaultInactivityThresholdSeconds;

            if (root != null) SetSlidersToCurrentValues();
        }

        private void SetSlidersToCurrentValues()
        {
            if (audioSettings == null) return;

            // SetValueWithoutNotify, not .value: assigning the value raises a ChangeEvent that runs
            // the setters below and writes straight back into the ScriptableObject — which in the
            // Editor means seeding the sliders dirties the asset on disk.
            masterVolume.SetValueWithoutNotify(audioSettings.masterVolume);
            dialogVolume.SetValueWithoutNotify(audioSettings.dialogVolume);
            musicVolume.SetValueWithoutNotify(audioSettings.musicVolume);
            sfxVolume.SetValueWithoutNotify(audioSettings.sfxVolume);

            // Was registered but never seeded, so it always showed the UXML default while the live
            // timeout was something else — and the next nudge jumped the reset time to that default.
            if (inactivityThreshold != null && inputDispatcher != null)
                inactivityThreshold.SetValueWithoutNotify(inputDispatcher.secondsUntilGameReset);

            if (subtitles != null)
                subtitles.SetValueWithoutNotify(audioSettings.subtitlesEnabled);
        }
        
        #endregion

        public void Hide()
        {
            IsVisible = false;
            if (root != null) root.visible = false;
        }

        public void Show()
        {
            IsVisible = true;
            if (root == null) return;
            root.visible = true;
            SetSlidersToCurrentValues();
        }
    }
}