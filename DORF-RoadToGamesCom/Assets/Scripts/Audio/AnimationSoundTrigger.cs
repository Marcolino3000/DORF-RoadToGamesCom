using UnityEngine;

public class AnimationSoundTrigger : MonoBehaviour
{
    [Header("Wwise Events")]
    [SerializeField] private AK.Wwise.Event stepEvent;

    public void PlayStepSound()
    {
        stepEvent?.Post(gameObject);
    }
}
