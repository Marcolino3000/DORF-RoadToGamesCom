using UnityEngine;

namespace Audio
{
    public class AmbiencePlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip outsideAmbience;
        [SerializeField] private AudioClip insideAmbience;
        [SerializeField] private AK.Wwise.Event gardenAmbience;

        private Coroutine fadeCoroutine;
        [SerializeField] private float fadeDuration = 1.0f;

        private void Start()
        {
            RoomManager.OnRoomChanged += HandleRoomChanged;
            PlayAmbience(outsideAmbience);
        }

        private void HandleRoomChanged(bool isOutside)
        {
            AudioClip targetClip = isOutside ? outsideAmbience : insideAmbience;
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeToClip(targetClip, fadeDuration));
        }

        private System.Collections.IEnumerator FadeToClip(AudioClip newClip, float duration)
        {
            if (audioSource.clip == newClip)
                yield break;
            float startVolume = audioSource.volume;
            // Fade out
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0, t / duration);
                yield return null;
            }
            audioSource.volume = 0;
            audioSource.clip = newClip;
            audioSource.Play();
            // Fade in
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                audioSource.volume = Mathf.Lerp(0, startVolume, t / duration);
                yield return null;
            }
            audioSource.volume = startVolume;
        }

        private void PlayAmbience(AudioClip audioClip)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }
}