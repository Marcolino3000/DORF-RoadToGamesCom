using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class Questlog : MonoBehaviour
    {
        [SerializeField] private float countdownUntilShow;
        [SerializeField] private float showDuration;
        [SerializeField] private float animationDuration;
        [SerializeField] private Image logImage;
        [SerializeField] private RectTransform logRectTransform;
        [SerializeField] private bool wasShown;
        
        private Vector2 originalAnchoredPosition;
        private Vector2 offscreenAnchoredPosition;
        private bool isLogVisible;
        
        private void Awake()
        {
            if (logRectTransform == null)
                logRectTransform = logImage.GetComponent<RectTransform>();
            originalAnchoredPosition = logRectTransform.anchoredPosition;
            offscreenAnchoredPosition = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y + logRectTransform.rect.height + 100f);
            logRectTransform.anchoredPosition = offscreenAnchoredPosition;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if(scene.name == "Scene 2 from package")
                StartCoroutine(ShowQuestLog());
        }

        private void HandleMenuToggled(bool isMenuVisible)
        {
            if (isMenuVisible && isLogVisible) return;
            
            if (isMenuVisible && !isLogVisible)
            {
                Reset();
                return;
            }
            if (!isMenuVisible && !isLogVisible)
            {
                Reset();
                StartCoroutine(ShowQuestLog());
            }
        }

        private void Reset()
        {
            StopAllCoroutines();
            logRectTransform.anchoredPosition = offscreenAnchoredPosition;
            isLogVisible = false;
        }

        private IEnumerator ShowQuestLog()
        {
            if (wasShown) yield break;
            
            yield return StartCoroutine(StartCountdown(countdownUntilShow));
            yield return StartCoroutine(AnimateTransition(true));
            yield return StartCoroutine(StartCountdown(showDuration));
            wasShown = true;
            yield return StartCoroutine(AnimateTransition(false));
        }

        private IEnumerator AnimateTransition(bool show)
        {
            isLogVisible = show;
            
            float elapsed = 0f;
            Vector2 startPos = show ? offscreenAnchoredPosition : originalAnchoredPosition;
            Vector2 endPos = show ? originalAnchoredPosition : offscreenAnchoredPosition;

            if (show)
                logImage.enabled = true;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                logRectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            logRectTransform.anchoredPosition = endPos;

            if (!show)
                logImage.enabled = false;
        }

        private void SetQuestLogVisible(bool visible)
        {
            logImage.enabled = visible;
        }

        private IEnumerator StartCountdown(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
        }
    }
}