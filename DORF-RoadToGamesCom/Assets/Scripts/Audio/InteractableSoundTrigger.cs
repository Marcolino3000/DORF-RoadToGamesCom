using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Postet ein Wwise-Event, wenn der Spieler das Interactable benutzt.
    /// Gehört auf das GameObject mit dem <see cref="Interactable"/>.
    /// </summary>
    public class InteractableSoundTrigger : MonoBehaviour
    {
        [Header("Wwise Events")]
        [SerializeField] private AK.Wwise.Event interactionEvent;

        [Header("References")]
        [SerializeField] private Interactable interactable;

        private bool _isSubscribed;

        private void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponentInChildren<Interactable>();

            if (interactable == null)
            {
                Debug.LogError($"{nameof(InteractableSoundTrigger)} on {name}: no Interactable found. No sound will play.", this);
                return;
            }

            interactable.OnInteractionStarted += HandleInteractionStarted;
            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed)
                return;

            interactable.OnInteractionStarted -= HandleInteractionStarted;
            _isSubscribed = false;
        }

        private void HandleInteractionStarted(InteractionTriggerVia via, InteractableState state)
        {
            if (interactionEvent == null || !interactionEvent.IsValid())
            {
                Debug.LogWarning($"{nameof(InteractableSoundTrigger)} on {name}: no Wwise event assigned.", this);
                return;
            }

            interactionEvent.Post(gameObject);
        }
    }
}
