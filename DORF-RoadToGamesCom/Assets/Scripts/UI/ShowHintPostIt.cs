using System.Collections;
using Runtime.Scripts.Core;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Slides hint post-its into the HintLogCanvas from off-screen, holds them, slides them back out.
    ///
    /// Two hints share this component. The journal post-it is the one authored in Global.prefab and
    /// shows once its reaction has finished. The skip post-it is a runtime clone of it and shows when
    /// the first dialog with Paul starts, so the visitor learns about the spacebar the moment there is
    /// something to skip.
    /// </summary>
    public class ShowHintPostIt : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float showDuration;
        [SerializeField] private float animationDuration;
        [SerializeField] private string hintText;
        [SerializeField] private RectTransform logRectTransform;
        
        [Header("References")]
        [SerializeField] private Reaction reaction;
        [SerializeField] private TextMeshProUGUI text;

        [Header("Skip Hint")]
        [SerializeField] private string skipHintText = "Press \"Space\" to skip dialogue";
        [Tooltip("Offset gegenüber dem Journal-Post-It, damit sich beide nicht überdecken, falls sie gleichzeitig laufen.")]
        [SerializeField] private Vector2 skipHintOffset = Vector2.zero;
        [Tooltip("Reaction, die den ersten Paul-Dialog startet. Leer lassen: wird über den Pfad darunter aus Resources geladen.")]
        [SerializeField] private Reaction skipHintReaction;
        [SerializeField] private string skipHintReactionResourcePath =
            "ScriptableObjects/Reactions/Dialog/Scene 2 - DialogWithPaulReaction";

        private PostIt _journalPostIt;
        private PostIt _skipPostIt;
        private Reaction _resolvedSkipHintReaction;

        private void Start()
        {
            // Clone first: the constructor of PostIt parks the original off-screen, and the clone is
            // supposed to inherit the authored on-screen position, not the parked one.
            var skipRect = CreateSkipPostIt();

            reaction.OnReactionFinished += OnReactionFinished;
            _journalPostIt = new PostIt(logRectTransform);

            if (skipRect == null)
                return;

            _skipPostIt = new PostIt(skipRect);
            _resolvedSkipHintReaction.OnStartDialog += OnSkipHintDialogStarted;
        }

        private void OnDestroy()
        {
            if (reaction != null)
                reaction.OnReactionFinished -= OnReactionFinished;

            if (_resolvedSkipHintReaction != null)
                _resolvedSkipHintReaction.OnStartDialog -= OnSkipHintDialogStarted;
        }

        /// <summary>
        /// Duplicates the authored post-it, swaps its text and returns the clone. Null when the
        /// reaction that starts the first Paul dialog cannot be resolved — the journal hint keeps working.
        /// </summary>
        private RectTransform CreateSkipPostIt()
        {
            _resolvedSkipHintReaction = skipHintReaction != null
                ? skipHintReaction
                : Resources.Load<Reaction>(skipHintReactionResourcePath);

            if (_resolvedSkipHintReaction == null)
            {
                Debug.LogWarning($"ShowHintPostIt: no Reaction at '{skipHintReactionResourcePath}', skip hint stays hidden.", this);
                return null;
            }

            var clone = Instantiate(logRectTransform, logRectTransform.parent, false);
            clone.name = logRectTransform.name + " Skip";
            clone.anchoredPosition = logRectTransform.anchoredPosition + skipHintOffset;

            var cloneText = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (cloneText != null)
                cloneText.text = skipHintText;
            else
                Debug.LogWarning("ShowHintPostIt: cloned post-it has no TextMeshProUGUI, skip hint stays empty.", this);

            return clone;
        }

        private void OnReactionFinished(bool completed)
        {
            Show(_journalPostIt);
        }

        // The reaction is reachable both by talking to Paul and by the door sequence walking him in, so
        // it can fire twice in one play-through. Restarting the animation is the sane answer either way.
        private void OnSkipHintDialogStarted()
        {
            Show(_skipPostIt);
        }

        private void Show(PostIt postIt)
        {
            if (postIt == null)
                return;

            if (postIt.Running != null)
                StopCoroutine(postIt.Running);

            postIt.Running = StartCoroutine(ShowHint(postIt));
        }

        private IEnumerator ShowHint(PostIt postIt)
        {
            yield return AnimateTransition(postIt, true);
            yield return new WaitForSeconds(showDuration);
            yield return AnimateTransition(postIt, false);

            postIt.Running = null;
        }

        private IEnumerator AnimateTransition(PostIt postIt, bool show)
        {
            float elapsed = 0f;
            // Start where the post-it currently is, not where it would be at rest: a hint that gets
            // re-triggered mid-slide then keeps moving instead of snapping. At rest both are the same.
            Vector2 startPos = postIt.RectTransform.anchoredPosition;
            Vector2 endPos = show ? postIt.OnScreenPosition : postIt.OffScreenPosition;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                postIt.RectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            postIt.RectTransform.anchoredPosition = endPos;
        }

        /// <summary>One post-it: its rect, the two positions it travels between, and its running animation.</summary>
        private class PostIt
        {
            public readonly RectTransform RectTransform;
            public readonly Vector2 OnScreenPosition;
            public readonly Vector2 OffScreenPosition;
            public Coroutine Running;

            public PostIt(RectTransform rectTransform)
            {
                RectTransform = rectTransform;
                OnScreenPosition = rectTransform.anchoredPosition;
                OffScreenPosition = new Vector2(OnScreenPosition.x, OnScreenPosition.y + 400f);
                rectTransform.anchoredPosition = OffScreenPosition;
            }
        }
    }
}
