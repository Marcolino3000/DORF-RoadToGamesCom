using System;
using System.Collections;
using System.Collections.Generic;
using Runtime.Scripts.Interactables;
using SceneManagement;
using UnityEngine;

namespace DefaultNamespace
{
    public class DoorFader : MonoBehaviour
    {
        [SerializeField] private List<Toggleable> doorToggles = new();
        [SerializeField] private float holdDuration = 0.5f;

        private Coroutine fadeCoroutine;

        private void Awake()
        {
            foreach (var toggle in doorToggles)
            {
                toggle.OnInteractionFeedback += HandleDoorToggle;
            }
        }

        private void HandleDoorToggle()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            fadeCoroutine = StartCoroutine(FadeSequence());
        }

        private IEnumerator FadeSequence()
        {
            var fader = SceneFader.Instance;
            
            if (fader == null)
                yield break;

            fader.StartFadeOut();

            while (fader.IsFadingOut)
                yield return null;

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);

            fader.StartFadeIn();

            while (fader.IsFadingIn)
                yield return null;

            fadeCoroutine = null;
        }
    }
}