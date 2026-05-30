using Runtime.Scripts.Core;
using UnityEngine;

public class HideJournalWhenFound : MonoBehaviour
{
    [SerializeField] private Reaction findJournalReaction;
    [SerializeField] private SpriteRenderer renderer;
    [SerializeField] private PolygonCollider2D collider;
    
    
    private void Start()
    {
        if (findJournalReaction == null)
        {
            Debug.LogError("FindJournalReaction not set on HideJournalWhenFound script!");
            return;
        }
        
        findJournalReaction.OnReactionFinished += OnFindJournalReactionFinished;
    }

    private void OnFindJournalReactionFinished(bool completed)
    {
        renderer.enabled = false;
        collider.enabled = false;
        // enabled = false;
    }
}
