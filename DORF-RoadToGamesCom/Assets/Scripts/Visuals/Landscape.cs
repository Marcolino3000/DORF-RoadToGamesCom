using System.Collections;
using UnityEngine;

namespace DefaultNamespace
{
    public class Landscape : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Range(0f, 1f)] private float speed;
        [SerializeField] private float slowDownDuration;
        
        [SerializeField] private MeshRenderer meshRenderer;

        private Material material;
        private Coroutine slowDownCoroutine;

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            material = meshRenderer.material;
        }

        private void Update()
        {
            ScrollMaterial();
        }

        private void ScrollMaterial()
        {
            Vector2 offset = material.mainTextureOffset;
            offset.x += speed * Time.deltaTime;

            material.mainTextureOffset = offset;
        }

        [ContextMenu("Slow Down")]
        public void SlowDown()
        {
            if (slowDownCoroutine != null)
            {
                StopCoroutine(slowDownCoroutine);
            }
            
            slowDownCoroutine = StartCoroutine(SlowDownRoutine());
        }
        
        private IEnumerator SlowDownRoutine()
        {
            float startSpeed = speed;
            float timeElapsed = 0f;

            while (timeElapsed < slowDownDuration)
            {
                speed = Mathf.Lerp(startSpeed, 0f, timeElapsed / slowDownDuration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            speed = 0f;
        }
    }
}