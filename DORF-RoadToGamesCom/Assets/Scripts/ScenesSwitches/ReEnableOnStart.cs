using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScenesSwitches
{
    public class ReEnableOnStart : MonoBehaviour
    {
        private IEnumerator Start()
        {
            //re-enable to take effect
            var volume = GetComponent<Volume>();
            yield return null;
            volume.enabled = false;
            volume.enabled = true;
        }
    }
}
