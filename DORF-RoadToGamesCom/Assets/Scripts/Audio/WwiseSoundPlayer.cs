using Runtime.Scripts.Interactables;
using UnityEngine;

namespace Audio
{
    public class WwiseSoundPlayer : MonoBehaviour
    {
        [Header("Wwise Events")]
        [SerializeField] private AK.Wwise.Event clickEvent;
        
        [Header("References")]
        [SerializeField] private Raycaster raycaster;


        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            // raycaster.OnClick += () => Play(clickEvent);
        }

        [ContextMenu("Play Event")]
        private void Play()
        {
            clickEvent.Post(gameObject);
        }
    }
}