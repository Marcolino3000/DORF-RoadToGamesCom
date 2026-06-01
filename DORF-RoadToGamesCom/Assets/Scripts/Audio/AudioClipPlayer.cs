using System;
using System.Collections;
using Audio;
using Nodes;
using Nodes.Decorator;
using Tree;
using UnityEngine;
using UnityEngine.Audio;

namespace DefaultNamespace
{
    public class AudioClipPlayer : MonoBehaviour
    {
        public static event Action FinishedPlaying;

        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioClip paulTalksThroughDoorClip;
        [SerializeField] private MarkerManager markerManager;
        [SerializeField] private InGameAudioSettings audioSettings;

        private float currentClipVolume;
        private Coroutine clipWatcher;
        private float playerOptionFactor = 0.125f;

        private void Update()
        {
            if (!audioSource.isPlaying) 
                return;
            
            var playheadSample = audioSource.timeSamples;
            markerManager.CheckPlayhead(audioSource.clip, playheadSample);
        }

        private void PlayClip(Node node)
        {
            if(node.AudioClip == null) 
                return;
            
            markerManager?.ResetPlayheadCheck();

            var defaultSnapshot = mixer.FindSnapshot("Default");
            defaultSnapshot.TransitionTo(0f);

            if(node.AudioClip == paulTalksThroughDoorClip)
            {
                var snapshot = mixer.FindSnapshot("Lowpass");
                if (snapshot != null)
                {
                    snapshot.TransitionTo(0f);
                }
                else
                {
                    Debug.LogWarning("Snapshot " + snapshot.name + " not found");
                }
            }
            
            playerOptionFactor = node is PlayerDialogOption ? 0.25f : 0.125f;
            currentClipVolume = node.ClipVolume;
            audioSource.volume = currentClipVolume * audioSettings.GetDialogVolume() ;
            // audioSource.volume *= node.ClipVolume * audioSettings.GetDialogVolume();
            
            audioSource.clip = node.AudioClip;
            
            audioSource.Play();
            
            if (clipWatcher != null)
                StopCoroutine(clipWatcher);
            clipWatcher = StartCoroutine(WatchClipEnd(node));
        }
        
        private IEnumerator WatchClipEnd(Node node)
        {
            yield return new WaitWhile(() => audioSource.isPlaying);

            if (audioSource.clip == node.AudioClip)
            {
                FinishedPlaying?.Invoke();
            }

            clipWatcher = null;
        }
        
        
        private void Awake()
        {
            DialogTreeRunner.DialogNodeSelected += PlayClip;
            audioSettings.OnDialogVolumeChanged += SetDialogVolume;
            // markerManager.OnMarkerReached += OnMarkerReached;

            Debug.Log(AudioSettings.GetConfiguration().sampleRate);
        }

        private void SetDialogVolume(float volume)
        {
            audioSource.volume = volume * currentClipVolume;
        }

        // private static void OnMarkerReached(MarkerType markerType)
        // {
        //     MarkerReached?.Invoke(markerType);
        // }
    }
}