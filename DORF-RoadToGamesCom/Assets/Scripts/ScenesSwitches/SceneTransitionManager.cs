using System.Collections;
using DefaultNamespace;
using SceneManagement;
using UI;
using UnityEngine;

namespace ScenesSwitches
{
    /// <summary>
    /// Runs Scene 1 out: music, the camera move through the train window, the title, Scene 2.
    ///
    /// The music is the clock. It is posted the moment the last voice memo has played out, and
    /// every step after it is timed off that post. Nothing waits for the landscape any more — the
    /// painting scrolls where it scrolls, and the camera always leaves the window the same distance
    /// into the music.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("From the music starting to the train braking. The phone stays open and usable " +
                 "for it, so this is also the beat the visitor gets after the last memo. " +
                 "0 brakes immediately.")]
        [SerializeField] private float secondsBetweenMusicAndCameraZoom = 10f;

        [Tooltip("From the train braking to the camera zooming through the window. Give the " +
                 "landscape time to actually come to a stop before the camera leaves. 0 zooms " +
                 "straight after the brake.")]
        [SerializeField] private float secondsBetweenSlowDownAndCameraZoom = 2f;

        [SerializeField] private float secondsBeforeTitleFadeIn;
        [SerializeField] private float titleScreenDuration;

        [Header("References")]
        [SerializeField] private TrainMover trainMover;
        [SerializeField] private TitleFadeIn titleFadeIn;
        [SerializeField] private Smartphone smartphone;
        [SerializeField] private SceneSwapManager sceneSwapManager;
        [SerializeField] private Landscape landscape;

        [Tooltip("Posted as soon as the voice memos have finished. MUS_Scene2_Start.")]
        [SerializeField] private AK.Wwise.Event musicCue;

        [Tooltip("Optional second event, posted in the same frame as the music. For anything that " +
                 "has to start with it — an ambience switch, a stinger, a state change. Leave " +
                 "empty if not needed.")]
        [SerializeField] private AK.Wwise.Event additionalCue;

        [ContextMenu("Start Transition")]
        public void StartTransition()
        {
            StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            if (musicCue != null)
                musicCue.Post(gameObject);

            if (additionalCue != null)
                additionalCue.Post(gameObject);

            // The wait everything hangs off: the train does not brake until the music has had its
            // run-up, so moving this moves the whole ending.
            if (secondsBetweenMusicAndCameraZoom > 0f)
                yield return new WaitForSeconds(secondsBetweenMusicAndCameraZoom);

            landscape.SlowDown();

            smartphone.Close();

            // The brake is a ramp, not a cut. Leaving the window while the panorama is still
            // sliding reads as the train never having stopped, so wait it out.
            if (secondsBetweenSlowDownAndCameraZoom > 0f)
                yield return new WaitForSeconds(secondsBetweenSlowDownAndCameraZoom);

            trainMover.MoveTowardsCamera();

            yield return new WaitForSeconds(secondsBeforeTitleFadeIn);

            titleFadeIn.FadeIn();

            yield return new WaitForSeconds(titleScreenDuration);

            SceneSwapManager.PreloadAndChangeScene("Scene 2");
        }

        private void Awake()
        {
            smartphone.OnVoiceChainFinished += StartTransition;
        }

        private void OnDestroy()
        {
            if (smartphone != null)
                smartphone.OnVoiceChainFinished -= StartTransition;
        }
    }
}
