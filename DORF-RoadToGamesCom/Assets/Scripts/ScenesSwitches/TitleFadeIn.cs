using System.Collections;
using UnityEngine;

namespace ScenesSwitches
{
    public class TitleFadeIn : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float duration = 2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Coroutine fadeRoutine;

        [ContextMenu("Fade In")]
        public void FadeIn()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                Debug.LogWarning("TitleFadeIn: No SpriteRenderer assigned or found.", this);
                return;
            }

            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);

            fadeRoutine = StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            float elapsed = 0f;
            Color color = spriteRenderer.color;

            color.a = 0f;
            spriteRenderer.color = color;

            while (elapsed < duration)
            {
                float normalizedTime = elapsed / duration;
                color.a = Mathf.Clamp01(fadeCurve.Evaluate(normalizedTime));
                spriteRenderer.color = color;

                elapsed += Time.deltaTime;
                yield return null;
            }

            color.a = 1f;
            spriteRenderer.color = color;
            fadeRoutine = null;
        }
    }
}
