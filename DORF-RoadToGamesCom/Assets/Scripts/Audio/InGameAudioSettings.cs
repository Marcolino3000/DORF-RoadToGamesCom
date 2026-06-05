    using System;
using UnityEngine;

namespace Audio
{
    [CreateAssetMenu(menuName = "Settings/InGameAudioSettings")]
    public class InGameAudioSettings : ScriptableObject
    {
        public event Action<float> OnDialogVolumeChanged;
        
        [Range(0f, 1f)]
        public float masterVolume = 1f;
        
        [Range(0f, 1f)]
        public float musicVolume = 1f;
        
        [Range(0f, 1f)]
        public float sfxVolume = 1f;
        
        [Range(0f, 1f)]
        public float dialogVolume = 1f;

        public int inactivityThresholdSeconds;
        
        public float GetDialogVolume()
        {
            return dialogVolume * masterVolume;
        }
        
        public void SetMasterVolume(float value)
        {
            masterVolume = value;
            UpdateWwiseRTPCs();
            OnDialogVolumeChanged?.Invoke(value * masterVolume);
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
            OnDialogVolumeChanged?.Invoke(value * masterVolume);
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