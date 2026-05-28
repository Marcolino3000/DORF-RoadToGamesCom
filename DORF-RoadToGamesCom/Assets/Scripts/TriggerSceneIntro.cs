using Runtime.Scripts.Core;
using UnityEngine;

namespace DefaultNamespace
{
    public class TriggerSceneIntro : MonoBehaviour
    {
        [SerializeField] private Reaction reactionToTrigger;

        private void Awake()
        {
            reactionToTrigger.Execute();
        }
    }
}