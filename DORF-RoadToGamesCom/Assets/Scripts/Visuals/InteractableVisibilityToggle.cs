using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Blendet ein zweites Objekt aus, wenn der Spieler das Interactable anklickt, und beim nächsten
    /// Klick wieder ein: der Deckel der Regentonne im Garten.
    /// Gehört auf das GameObject mit dem <see cref="Interactable"/> (also auf das Sprite-Objekt, unter
    /// dem das Interactable-Prefab hängt).
    ///
    /// Der Schaltzustand liegt absichtlich hier und nicht in einem ScriptableObject: die Komponente
    /// entsteht bei jedem Szenenladen neu, also liegt der Deckel nach einem Kiosk-Reset wieder drauf.
    /// Ein <see cref="Toggleable"/> würde seinen ToggleState dagegen auf dem Asset behalten - im
    /// Editor sogar über die Play-Session hinaus.
    /// </summary>
    public class InteractableVisibilityToggle : MonoBehaviour, ISceneSetupCallbackReceiver
    {
        [Header("Settings")]
        [Tooltip("Ist das Zielobjekt zu sehen, bevor zum ersten Mal geklickt wurde?")]
        [SerializeField] private bool visibleAtStart = true;

        [Header("References")]
        [Tooltip("Das Objekt, das jeder Klick ein- und ausblendet.")]
        [SerializeField] private GameObject targetObject;

        [Tooltip("Optional. Bleibt der Slot leer, wird das Interactable in den Kindern gesucht.")]
        [SerializeField] private Interactable interactable;

        private bool _isSubscribed;

        private void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponentInChildren<Interactable>();

            if (interactable == null)
            {
                Debug.LogError($"{nameof(InteractableVisibilityToggle)} on {name}: no Interactable found. Nothing will toggle.", this);
                return;
            }

            if (targetObject == null)
            {
                Debug.LogError($"{nameof(InteractableVisibilityToggle)} on {name}: no target object assigned. Nothing will toggle.", this);
                return;
            }

            targetObject.SetActive(visibleAtStart);

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
            targetObject.SetActive(!targetObject.activeSelf);
        }

        public void OnSceneSetup()
        {
            if (targetObject != null)
                targetObject.SetActive(visibleAtStart);
        }
    }
}
