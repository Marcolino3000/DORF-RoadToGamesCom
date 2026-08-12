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
