using System;
using Runtime.Scripts.PlayerInput;
using Tree;
using UnityEngine;

namespace DefaultNamespace
{
    public class MovementDisabler : MonoBehaviour
    {
        
        public bool disableMovementOnDialog = true;
        private void OnEnable()
        {
            DialogTreeRunner.OnDialogRunningStatusChanged += HandleDialogStatusChanged;
        }

        private void HandleDialogStatusChanged(bool isRunning, DialogTree tree)
        {
            if(!disableMovementOnDialog) 
                return;
            
            PlayerController.EnableMovement(!isRunning);
        }
    }
}