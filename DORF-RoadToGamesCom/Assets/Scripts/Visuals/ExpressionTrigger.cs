using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace DefaultNamespace
{
    public class ExpressionTrigger : SerializedMonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MarkerManager markerManager;
        [SerializeField] private string characterName;

        private void OnEnable()
        {
            markerManager.OnMarkerReached += TriggerExpression;
            AudioClipPlayer.FinishedPlaying += ResetExpressionToDefault;
            characterName = name;
        }

        private void OnDisable()
        {
            markerManager.OnMarkerReached -= TriggerExpression;
            AudioClipPlayer.FinishedPlaying -= ResetExpressionToDefault;
        }

        private void ResetExpressionToDefault()
        {
            animator.SetInteger("expState", 1);
        }

        private void TriggerExpression(MarkerManager.MarkerType type, string characterName)
        {
            // Debug.Log("triggered expression: " + type);
            if (type == MarkerManager.MarkerType.Paragraph)
                return;

            if (!CheckForCharacter(characterName))
                return;
            
            animator.SetInteger("expState", (int)type);
            
        }

        private bool CheckForCharacter(string characterNameArg)
        {
            if (characterNameArg.IsNullOrWhitespace())
            {
                Debug.LogError("CharacterName was not set!");
                return false;
            }

            return characterName == characterNameArg;
        }
    }
}
