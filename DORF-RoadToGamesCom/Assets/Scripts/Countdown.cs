using System;
using System.Collections;
using UnityEngine;

namespace DefaultNamespace
{
    public class Countdown : MonoBehaviour
    {
        public static event Action OnCountdownFinished;
        
        [SerializeField] private float duration;
        
        private float _currentTime;
        
        public void StartCountdown()
        {
            StartCoroutine(CountdownCoroutine());
        }

        private IEnumerator CountdownCoroutine()
        {
            while (_currentTime < duration)
            {
                _currentTime += Time.deltaTime;
                yield return null;
            }
            
            OnCountdownFinished?.Invoke();
            _currentTime = 0f;
        }
    }
}