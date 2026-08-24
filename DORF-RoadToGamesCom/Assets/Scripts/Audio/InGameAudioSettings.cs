    using System;
using Runtime.Scripts.Interactables;
using UI;
using UnityEngine;

namespace Audio
{
    [CreateAssetMenu(menuName = "Settings/InGameAudioSettings")]
    public class InGameAudioSettings : ScriptableObject, ISceneSetupCallbackReceiver
    {
        public event Action<float> OnDialogVolumeChanged;

        // Runtime state. The settings menu writes straight into these, and because this is a
        // ScriptableObject they survive scene loads — and in the Editor they are written to disk.
        // The volumes are seeded from the defaults below once per launch and then belong to
        // whoever moved the sliders last; never author intended values here.
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Range(0f, 1f)]
        public float musicVolume = 1f;

        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Range(0f, 1f)]
        public float dialogVolume = 1f;

        public int inactivityThresholdSeconds;

        /// <summary>
        /// Runtime state like the volumes, and it survives scene loads for the same reason — a
        /// visitor who switches the subtitles off keeps them off while walking from room to room.
        /// Unlike the volumes it *is* put back on the game reset (GameResetter.ResetSubtitles), so
        /// the next visitor always starts with subtitles on.
        /// </summary>
        public bool subtitlesEnabled = true;

        [Header("Exhibition defaults")]
        [Tooltip("What the values above are seeded from once per launch. The inactivity timeout is " +
                 "additionally restored on every scene load and on the reset, the volumes are not. " +
                 "These are the authored settings — edit these, not the runtime ones.")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultDialogVolume = 1f;
        [SerializeField] private int defaultInactivityThresholdSeconds = 180;
        [SerializeField] private bool defaultSubtitlesEnabled = true;

        /// <summary>
        /// The authored timeout. Read this rather than <see cref="inactivityThresholdSeconds"/> when
        /// restoring, so it does not matter whether this object was reset first.
        /// </summary>
        public int DefaultInactivityThresholdSeconds => defaultInactivityThresholdSeconds;

        /// <summary>
        /// What the subtitles are put back to on the game reset. On for the exhibition.
        /// </summary>
        public bool DefaultSubtitlesEnabled => defaultSubtitlesEnabled;

        /// <summary>
        /// True from the first <see cref="ApplyLaunchDefaultsOnce"/> of the process on. Static, so a
        /// build starts every run from the authored defaults while a running game — scene loads and
        /// the inactivity reset included — never touches the volumes again.
        /// </summary>
        private static bool launchDefaultsApplied;

        /// <summary>
        /// Statics survive entering Play mode when domain reloading is switched off, which would
        /// carry the last session's volumes into the next one. SubsystemRegistration runs before
        /// every Play session either way.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLaunchDefaultsFlag()
        {
            launchDefaultsApplied = false;
        }

        /// <summary>
        /// Runs on every scene load and on the inactivity reset.
        ///
        /// The volumes are deliberately *not* restored here: a level set at the kiosk has to hold
        /// for the rest of the day, and resetting them mid-run would undo it on the next scene swap.
        /// They are seeded from the defaults once per launch instead — which in a build is what the
        /// asset ships with anyway, and in the Editor keeps the last play session's values (this is
        /// a ScriptableObject, so they were written to disk) from becoming the next run's start.
        ///
        /// The inactivity timeout is not an audio setting and keeps the old rule: a visitor who
        /// drags that slider must not be able to switch the kiosk's reset off for everyone after
        /// them.
        /// </summary>
        public void OnSceneSetup()
        {
            ApplyLaunchDefaultsOnce();

            inactivityThresholdSeconds = defaultInactivityThresholdSeconds;

            // Re-applied, not reset: this method also runs on every scene load, and switching the
            // subtitles back on when a visitor walks through a door would undo their choice. The
            // reset to the default happens in GameResetter, which knows a play-through has ended.
            SubtitleDisplay.Apply(subtitlesEnabled);

            // The values stay as they are, but the sound engine is fed again: RTPC values are global
            // engine state rather than ours, so this is what keeps Wwise on the current settings
            // after an engine or bank reload.
            UpdateWwiseRTPCs();
            OnDialogVolumeChanged?.Invoke(GetDialogVolume());
        }

        /// <summary>
        /// Seeds the runtime values from the authored defaults, once per application launch. Safe to
        /// call from every scene-setup receiver, so it does not matter which of them runs first.
        /// </summary>
        public void ApplyLaunchDefaultsOnce()
        {
            if (launchDefaultsApplied)
                return;

            launchDefaultsApplied = true;
            RestoreDefaults();
        }

        /// <summary>
        /// Full manual reset to the authored values. Nothing in a running game calls this any more —
        /// see <see cref="OnSceneSetup"/>.
        /// </summary>
        public void RestoreDefaults()
        {
            masterVolume = defaultMasterVolume;
            musicVolume = defaultMusicVolume;
            sfxVolume = defaultSfxVolume;
            dialogVolume = defaultDialogVolume;
            inactivityThresholdSeconds = defaultInactivityThresholdSeconds;
            subtitlesEnabled = defaultSubtitlesEnabled;

            SubtitleDisplay.Apply(subtitlesEnabled);
            UpdateWwiseRTPCs();
            OnDialogVolumeChanged?.Invoke(GetDialogVolume());
        }

        public float GetDialogVolume()
        {
            return dialogVolume * masterVolume;
        }
        
        public void SetMasterVolume(float value)
        {
            masterVolume = value;
            UpdateWwiseRTPCs();
            // masterVolume has just been assigned value, so the old "value * masterVolume" squared
            // the master and dropped dialogVolume entirely.
            OnDialogVolumeChanged?.Invoke(GetDialogVolume());
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = value;
            UpdateWwiseRTPCs();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = value;
            UpdateWwiseRTPCs();
        }

        /// <summary>
        /// The settings menu's subtitle switch. Writes the value and puts the subtitle container in
        /// that state right away, so it also takes effect in the middle of a running dialog.
        /// </summary>
        public void SetSubtitlesEnabled(bool value)
        {
            subtitlesEnabled = value;
            SubtitleDisplay.Apply(value);
        }

        public void SetDialogVolume(float value)
        {
            dialogVolume = value;
            OnDialogVolumeChanged?.Invoke(GetDialogVolume());
        }

        private void UpdateWwiseRTPCs()
        {
            AkUnitySoundEngine.SetRTPCValue("VOL_Master", masterVolume * 100f);
            AkUnitySoundEngine.SetRTPCValue("VOL_Music", musicVolume * 100f * masterVolume);
            AkUnitySoundEngine.SetRTPCValue("VOL_SFX", sfxVolume * 100f * masterVolume);
        }

        #region Setup
        private void OnValidate()
        {
            UpdateWwiseRTPCs();
        }

        private void OnEnable()
        {
            UpdateWwiseRTPCs();
        }

        #endregion
    }
}