using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Pins Unity's audio output to a fixed sample rate so dialog/music clips
    /// always play at the correct pitch, regardless of the player's output device.
    ///
    /// With the default project setting (System Sample Rate = 0) Unity follows
    /// whatever rate the connected output device reports. When that rate differs
    /// from the clips' rate (every source clip in Assets/AudioFiles is 48 kHz) —
    /// or when Wwise and Unity disagree about the device rate — playback pitch
    /// shifts. Locking the output to 48 kHz keeps Unity's pipeline matched to the
    /// clips; the OS does the final, pitch-correct conversion to whatever the
    /// hardware actually runs at.
    ///
    /// The project's System Sample Rate is also set to 48000 (AudioManager.asset),
    /// so startup is normally already correct and no reset happens. This guard only
    /// kicks in when the player swaps output device at runtime.
    /// </summary>
    public static class AudioOutputSampleRateLock
    {
        // Matches the native sample rate of every source clip and Wwise's default.
        private const int TargetSampleRate = 48000;

        // Guards against the re-entrant OnAudioConfigurationChanged callback that
        // AudioSettings.Reset() raises while we are applying the change.
        private static bool applying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Apply();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private static void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!applying)
                Apply();
        }

        private static void Apply()
        {
            var config = AudioSettings.GetConfiguration();
            if (config.sampleRate == TargetSampleRate)
                return;

            config.sampleRate = TargetSampleRate;

            applying = true;
            // Re-initialises the audio output. Currently playing AudioSources stop,
            // which is why this is expected to run at startup or on rare device swaps.
            bool success = AudioSettings.Reset(config);
            applying = false;

            if (!success)
                Debug.LogWarning(
                    $"[AudioOutputSampleRateLock] Failed to set output sample rate to {TargetSampleRate} Hz.");
        }
    }
}
