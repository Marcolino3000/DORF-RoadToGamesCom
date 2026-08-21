using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Postet ein Wwise-Event, wenn der Spieler das Interactable benutzt.
    /// Gehört auf das GameObject mit dem <see cref="Interactable"/>.
    ///
    /// Ist zusätzlich ein Aus-Event gesetzt, wird das Interactable zum Schalter: der erste Klick
    /// postet das An-Event, der zweite das Aus-Event, und so weiter. Genau dafür gibt es im
    /// Wwise-Projekt Paare wie MUS_Schlager_Start / MUS_Schlager_Stop — das Radio in der Küche.
    /// Bleibt der Slot leer, postet jeder Klick dasselbe Event wie bisher.
    ///
    /// Der Schaltzustand liegt absichtlich hier und nicht in einem ScriptableObject: die Komponente
    /// entsteht bei jedem Szenenladen neu, also fängt der Kiosk nach einem Reset wieder mit "aus"
    /// an. Die Musik selbst hört von allein auf, weil Wwise beim Zerstören des GameObjects alles
    /// abmeldet, was auf ihm spielt.
    /// </summary>
    public class InteractableSoundTrigger : MonoBehaviour
    {
        [Header("Wwise Events")]
        [SerializeField] private AK.Wwise.Event interactionEvent;

        [Tooltip("Optional. Gesetzt heißt: jeder zweite Klick postet dieses Event statt des oberen.")]
        [SerializeField] private AK.Wwise.Event interactionEventOff;

        [Header("References")]
        [SerializeField] private Interactable interactable;

        private bool _isSubscribed;
        private bool _offEventIsNext;

        private bool TogglesTwoEvents => interactionEventOff != null && interactionEventOff.IsValid();

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
            var eventToPost = _offEventIsNext ? interactionEventOff : interactionEvent;

            if (eventToPost == null || !eventToPost.IsValid())
            {
                Debug.LogWarning($"{nameof(InteractableSoundTrigger)} on {name}: no Wwise event assigned.", this);
                return;
            }

            eventToPost.Post(gameObject);

            if (TogglesTwoEvents)
                _offEventIsNext = !_offEventIsNext;
        }
    }
}
