using System.Collections;
using Runtime.Scripts.Core;
using SceneManagement;
using UI;
using UnityEngine;

namespace DefaultNamespace
{
    public class TitleSequenceTrigger : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float durationBeforeTitleScreen;
        [SerializeField] private float titleScreenDuration;

        [Header("References")]
        [SerializeField] private Reaction marianneSprachiReaction;
        [SerializeField] private UnityEngine.UI.Image titleScreenImage;
        [SerializeField] private Smartphone smartphone;

        public void StartSprachiDialog()
        {
            marianneSprachiReaction.Execute();
        }
        
        private void Awake()
        {
            // marianneSprachiReaction.OnReactionFinished += OnSprachiFinished;
        }

        private void OnSprachiFinished(bool completed)
        {
            StartCoroutine(TriggerTitleScreenAndScene2());
        }

        private IEnumerator TriggerTitleScreenAndScene2()
        {
            yield return new WaitForSeconds(durationBeforeTitleScreen);
            
            smartphone.Close();
            
            while(SceneFader.Instance.IsFadingOut)
                yield return null;
            
            // titleScreenImage.enabled = true;
            
            while (SceneFader.Instance.IsFadingIn)
                yield return null;
            
            yield return new WaitForSeconds(titleScreenDuration);
            
            // marlene?.SetActive(true);
            
            SceneSwapManager.ChangeScene("Scene 2");
        }
    }
}