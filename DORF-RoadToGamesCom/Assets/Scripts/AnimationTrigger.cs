using Runtime.Scripts.Core;
using Runtime.Scripts.PlayerInput;
using UnityEngine;

public class AnimationTrigger : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private MoveByClick moveByClick;

    private void OnEnable()
    {
        _playerController.OnMovementStarted += TriggerAnimation;
        _playerController.OnMovementEnded += StopAnimation;
        
        moveByClick.OnNavMeshMovementStarted += TriggerAnimation;
        moveByClick.OnNavMeshMovementEnded += StopAnimation;
    }

    [ContextMenu("Trigger Animation")]
    private void TriggerAnimation(MoveDirection moveDirection)
    {
        animator.SetBool("isWalking", true);
        
        FlipSprite(moveDirection);
    }

    private void FlipSprite(MoveDirection moveDirection)
    {
        transform.rotation = moveDirection switch
        {
            MoveDirection.Left => Quaternion.Euler(0, 0, 0),
            MoveDirection.Right => Quaternion.Euler(0, 180, 0),
            _ => transform.rotation
        };
    }

    [ContextMenu("stop animation")]
    private void StopAnimation()
    {
        animator.SetBool("isWalking", false);
    }
}
