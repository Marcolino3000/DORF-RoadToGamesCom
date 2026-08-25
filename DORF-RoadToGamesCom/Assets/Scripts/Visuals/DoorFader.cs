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

        /// <summary>
        /// The Toggleables are assets and outlive the scene, this object does not. A handler left on
        /// them fires into a destroyed DoorFader on the next visit, and whatever it touches first
        /// throws - out of the Reaction that raised it, which is often a running ScriptedSequence.
        /// </summary>
        private void OnDestroy()
        {
            foreach (var toggle in doorToggles)
            {
                if (toggle != null)
                    toggle.OnInteractionFeedback -= HandleDoorToggle;
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