using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class GameManager : MonoBehaviour
    {
        public static event Action GameStarted;
        private void Awake()
        {
            GameStarted?.Invoke();
        }
    }
}