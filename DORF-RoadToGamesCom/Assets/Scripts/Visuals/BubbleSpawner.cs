using UnityEngine;

namespace DefaultNamespace
{
    public class BubbleSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int amount;
        
        [Header("References")]
        [SerializeField] private RectTransform rectTransform;
        
        
        public void SpawnBubbles()
        {
            
            
            for (int i = 0; i < amount; i++)
            {
                int randomX = Random.Range(0, (int)rectTransform.rect.width);
                int randomY = Random.Range(0, (int)rectTransform.rect.height);
                Vector2 spawnPosition = new Vector2(randomX, randomY);
                
                
                
            }
        }
    }
}