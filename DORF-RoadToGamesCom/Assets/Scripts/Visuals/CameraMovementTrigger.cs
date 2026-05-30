using DefaultNamespace;
using Runtime.Scripts.Animation;
using Runtime.Scripts.Interactables;
using Tree;
using UnityEngine;
using CharacterData = Core.CharacterData;

public class CameraMovementTrigger : MonoBehaviour
{
    [SerializeField] private TiltCameraMovement camMovement;
    [SerializeField] private bool currentStatus;
    [SerializeField] private CharacterData commentCharacter;
    [SerializeField] private float dialogToggleCooldown = 0.75f; // seconds
    [SerializeField] private float lastDialogToggleTime = -100f;

    private void Awake()
    {
        camMovement = GetComponent<TiltCameraMovement>();
        DialogTreeRunner.OnDialogRunningStatusChanged += OnDialogRunningStatusChanged;
    }

    private void OnDialogRunningStatusChanged(bool isRunning, DialogTree tree)
    {
        // if (Time.time - lastDialogToggleTime < dialogToggleCooldown)
        //     return;
        
        if (currentStatus && isRunning) 
            return;

        if (tree == null)
        {
            Debug.LogWarning("No Tree Set. Returning early");
            return;
        }

        if (tree.Blackboard.CharacterData == null)
        {
            Debug.LogError("CharacterData not set in blackboard!");
            return;
        }

        if (tree.Blackboard.CharacterData == commentCharacter)
            return;

        var interactable = tree.Blackboard.CharacterData.Interactable;

        if (interactable == null)
        {
            Debug.LogError("Interactable not set in CharacterData!");
            return;
        }

        if (interactable is not InteractableState interactableState)
        {
            Debug.LogError("CharacterData interactable was not an interactableState");
            return;
        }

        // if(currentStatus != isRunning)
        currentStatus = isRunning;
        camMovement.ToggleDialogMode(isRunning, interactableState.Interactable.transform.position);
        lastDialogToggleTime = Time.time;
    }
}
