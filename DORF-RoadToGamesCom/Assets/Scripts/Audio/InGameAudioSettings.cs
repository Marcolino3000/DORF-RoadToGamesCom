    using System;
using Runtime.Scripts.Interactables;
using UnityEngine;

namespace Audio
{
    [CreateAssetMenu(menuName = "Settings/InGameAudioSettings")]
    public class InGameAudioSettings : ScriptableObject, ISceneSetupCallbackReceiver
    {
        public event Action<float> OnDialogVolumeChanged;

        // Runtime state. The settings menu writes straight into these, and because this is a
        // ScriptableObject they survive scene loads — and in the Editor they are written to disk.
        // RestoreDefaults puts them back; never author intended values here.
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Range(0f, 1f)]
        public float musicVolume = 1f;

        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Range(0f, 1f)]
        public float dialogVolume = 1f;

        public int inactivityThresholdSeconds;

        [Header("Exhibition defaults")]
        [Tooltip("What the values above are restored to on every scene load and on the inactivity " +
                 "reset. These are the authored settings — edit these, not the runtime ones.")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultDialogVolume = 1f;
        [SerializeField] private int defaultInactivityThresholdSeconds = 180;

        /// <summary>
        /// The authored timeout. Read this rather than <see cref="inactivityThresholdSeconds"/> when
        /// restoring, so it does not matter whether this object was reset first.
        /// </summary>
        public int DefaultInactivityThresholdSeconds => defaultInactivityThresholdSeconds;

        /// <summary>
        /// Runs on every scene load and on the inactivity reset. Without it a visitor who drags the
        /// master volume to zero leaves the kiosk silent for everyone after them, all day — and the
        /// game's opening beat is a voice message.
        /// </summary>
        public void OnSceneSetup()
        {
            RestoreDefaults();
        }

        public void RestoreDefaults()
        {
            masterVolume = defaultMasterVolume;
            musicVolume = defaultMusicVolume;
            sfxVolume = defaultSfxVolume;
            dialogVolume = defaultDialogVolume;
            inactivityThresholdSeconds = defaultInactivityThresholdSeconds;

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