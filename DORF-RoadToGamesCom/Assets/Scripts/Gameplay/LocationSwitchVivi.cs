using System;
using System.Collections;
using System.Collections.Generic;
using Runtime.Scripts.PlayerInput;
using UnityEngine;

namespace Runtime.Scripts.Interactables
{
    public class LocationSwitchVivi : MonoBehaviour
    {
        public enum VisibilityMode
        {
            Deactivate,      // SetActive(false)
            Activate,        // SetActive(true)
            MakeTransparent, // Alpha reduzieren
            MakeOpaque       // Alpha = 1
        }

        [Header("Mode Selection")]
        [SerializeField] private VisibilityMode visibilityMode;

        [Header("Objects")]
        [SerializeField] private List<GameObject> objectsToAffect;

        [Header("Transparency Settings")]
        [SerializeField] private float transparentAlpha = 0.4f;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() == null)
                return;

            StartCoroutine(StartPlayerEnteredCooldown());
        }

        private IEnumerator StartPlayerEnteredCooldown()
        {
            SwitchLocations();
            yield return new WaitForSeconds(1f);
        }

        private void SwitchLocations()
        {
            foreach (var obj in objectsToAffect)
            {
                if (obj == null) continue;

                switch (visibilityMode)
                {
                    case VisibilityMode.Deactivate:
                        SetActiveRecursive(obj, false);
                        break;

                    case VisibilityMode.Activate:
                        SetActiveRecursive(obj, true);
                        break;

                    case VisibilityMode.MakeTransparent:
                        SetTransparencyRecursive(obj, transparentAlpha);
                        break;

                    case VisibilityMode.MakeOpaque:
                        SetTransparencyRecursive(obj, 1f);
                        break;
                }
            }
        }

        private void SetActiveRecursive(GameObject obj, bool state)
        {
            var allChildren = GetAllChildrenExceptTrigger(obj);

            foreach (var child in allChildren)
            {
                child.SetActive(state);
            }
        }

        private void SetTransparencyRecursive(GameObject obj, float alpha)
        {
            var allChildren = GetAllChildrenExceptTrigger(obj);

            foreach (var child in allChildren)
            {
                var sprite = child.GetComponent<SpriteRenderer>();
                if (sprite == null) continue;

                var color = sprite.color;
                color.a = alpha;
                sprite.color = color;
            }
        }

        private List<GameObject> GetAllChildrenExceptTrigger(GameObject parent)
        {
            var result = new List<GameObject>();

            if (parent == null)
                return result;

            result.Add(parent);

            foreach (Transform child in parent.transform)
            {
                if (child.name == "Trigger" || child.name == "TriggerArea")
                    continue;

                result.AddRange(GetAllChildrenExceptTrigger(child.gameObject));
            }

            return result;
        }
    }
}