using System;
using Runtime.Scripts.Interactables;
using SceneManagement;
using UI;
using UnityEngine;

namespace DefaultNamespace
{
    public class GameReset : MonoBehaviour
    {
        [SerializeField] private GameObject marlene;
        [SerializeField] private Sauerteig sauerteig;
        [SerializeField] private Smartphone smartphone;
        [SerializeField] private SceneSwapManager sceneSwapManager;
        [SerializeField] private SceneSetup sceneSetup;

        private void OnGUI()
        {
            // if(GUI.Button())
            //actually einfach nur scene 1 neu laden?
        }
    }
}