using System.Collections;
using UnityEngine;

namespace Setup
{
    /// <summary>
    /// Fixes silent Wwise audio caused by the persistent Global prefab spawning too early.
    ///
    /// The Global prefab (which carries the AkAudioListener camera) is spawned by
    /// <see cref="Bootstrapper"/> at BeforeSceneLoad — before the in-scene AkInitializer
    /// boots the Wwise sound engine. AkAudioListener.Awake calls AkGameObj.Register()
    /// without checking that the engine is up: the engine call fails, but Register() has
    /// already latched its internal isRegistered flag, so every later attempt (the
    /// editor's init delegate, enabled-toggles) early-outs and the camera is never
    /// actually registered. With no default listener, everything is silent and Wwise
    /// logs "Unknown/Dead game object ID" every frame.
    ///
    /// Once the engine is initialized, this makes the stale flag truthful by registering
    /// the listener's game object directly with the sound engine, pushes its current
    /// position (AkGameObj caches it and would skip the update while the camera sits
    /// still), then re-toggles the listener so AddDefaultListener and SetScalingFactor
    /// run against a valid game object.
    ///
    /// Plain AkGameObj emitters spawned before engine init report themselves honestly as
    /// unregistered (only AkAudioListener.Awake force-calls Register()), so for them an
    /// enabled-toggle re-runs the full registration path. In the editor the integration's
    /// init delegate already handles these; in builds that delegate does not exist.
    /// </summary>
    public class WwiseListenerRegistrar : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(WwiseListenerRegistrar));
            DontDestroyOnLoad(host);
            host.AddComponent<WwiseListenerRegistrar>();
        }

        private IEnumerator Start()
        {
            // Wait until the sound engine is up (the AkInitializer lives in the scene).
            while (!AkUnitySoundEngine.IsInitialized())
                yield return null;

            // Give the integration a frame to run its own init-delegate registrations.
            yield return null;

            foreach (var akGameObj in FindObjectsByType<AkGameObj>(FindObjectsSortMode.None))
            {
                if (!akGameObj.enabled)
                    continue;

                if (!akGameObj.GameObjIsRegistered())
                {
                    // Honest "not registered": re-run the full OnEnable registration path
                    // (engine registration, position, environment data, listener list).
                    akGameObj.enabled = false;
                    akGameObj.enabled = true;
                }
                else if (akGameObj.GetComponent<AkAudioListener>() != null)
                {
                    // Possibly poisoned flag from AkAudioListener.Awake's pre-init
                    // Register() call. Registering engine-side makes it truthful;
                    // re-registering an already-known object is harmless.
                    var go = akGameObj.gameObject;
                    var t = go.transform;
                    AkUnitySoundEngine.RegisterGameObj(go, go.name);
                    AkUnitySoundEngine.SetObjectPosition(go, t.position, t.forward, t.up);
                }
            }

            foreach (var listener in FindObjectsByType<AkAudioListener>(FindObjectsSortMode.None))
            {
                if (!listener.enabled || !listener.isDefaultListener)
                    continue;

                // Re-run AkAudioListener.OnEnable → AddDefaultListener + SetScalingFactor,
                // now against a registered game object.
                listener.enabled = false;
                listener.enabled = true;
            }
        }
    }
}
