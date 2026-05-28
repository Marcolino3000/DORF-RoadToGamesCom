using UnityEngine;

namespace DefaultNamespace
{
    public class Landscape : MonoBehaviour
    {
        [SerializeField,Range(0.01f, 1)] private float speed;
        [SerializeField] private MeshRenderer meshRenderer;
        //Texture2D und wrap mode: repeat

        private Material material;

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
    }
}