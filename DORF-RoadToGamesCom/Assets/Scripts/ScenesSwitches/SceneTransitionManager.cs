using System.Collections;
using DefaultNamespace;
using SceneManagement;
using UI;
using UnityEngine;

namespace ScenesSwitches
{
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Pause between the last voice memo ending and the transition starting. The phone " +
                 "stays open and usable for it. 0 keeps the transition immediate.")]
        [SerializeField] private float secondsBeforeTransitionStarts;
        [SerializeField] private float secondsBeforeTitleFadeIn;
        [SerializeField] private float titleScreenDuration;

        [Header("Landscape cue")]
        [Tooltip("The train does not pull away until the landscape has scrolled this far, so it " +
                 "always stops at the same place in the painting. Panorama pixels on the " +
                 "Landscape's cue layer; the mark comes round once per lap.")]
        [SerializeField] private float transitionOffsetPixels;

        [Tooltip("How far ahead of that mark the music is posted, so it is already running when " +
                 "the title and Scene 2 arrive. Panorama pixels — at the default scroll speed " +
                 "169px is one second.")]
        [SerializeField] private float musicCueLeadPixels;

        [Tooltip("Run the transition anyway if the landscape has not reached the mark after this " +
                 "long, so the kiosk cannot sit on a chat that has already finished. 0 waits " +
                 "indefinitely.")]
        [SerializeField] private float maxOffsetWaitSeconds;

        [Header("References")]
        [SerializeField] private TrainMover trainMover;
        [SerializeField] private TitleFadeIn titleFadeIn;
        [SerializeField] private Smartphone smartphone;
        [SerializeField] private SceneSwapManager sceneSwapManager;
        [SerializeField] private Landscape landscape;

        [Tooltip("Posted at the music cue offset. MUS_Scene2_Start.")]
        [SerializeField] private AK.Wwise.Event musicCue;

        [ContextMenu("Start Transition")]
        public void StartTransition()
        {
            StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            // A beat between the second memo ending and the train pulling away, so the visitor is
            // not yanked out of the chat the moment the audio stops. Nothing has moved yet, so the
            // phone is still open here — closing it is the first thing the transition itself does.
            // Guarded rather than always yielded: WaitForSeconds(0) still costs a frame.
            if (secondsBeforeTransitionStarts > 0f)
                yield return new WaitForSeconds(secondsBeforeTransitionStarts);

            // From here the train keeps riding until the painting is at the agreed spot, so the
            // title always comes up over the same piece of landscape however long the chat took.
            // The music goes first, a stretch of landscape earlier, because it needs a run-up to
            // be in time — that lead is what fixes the gap between the two marks.
            yield return landscape.WaitForOffsetPixels(transitionOffsetPixels - musicCueLeadPixels,
                                                       maxOffsetWaitSeconds);

            if (musicCue != null)
                musicCue.Post(gameObject);

            yield return landscape.WaitForOffsetPixels(transitionOffsetPixels,
                                                       maxOffsetWaitSeconds);

            landscape.SlowDown();

            smartphone.Close();

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
