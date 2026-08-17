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

        [Header("References")]
        [SerializeField] private TrainMover trainMover;
        [SerializeField] private TitleFadeIn titleFadeIn;
        [SerializeField] private Smartphone smartphone;
        [SerializeField] private SceneSwapManager sceneSwapManager;
        [SerializeField] private Landscape landscape;

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
