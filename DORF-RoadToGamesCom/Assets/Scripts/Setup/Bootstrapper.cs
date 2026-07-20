using UnityEngine;

namespace Setup
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            // Boot the Wwise sound engine before the Global prefab spawns. Global's
            // AkAudioListener/AkGameObj/AkEvent components register and post from
            // Awake/OnEnable/Start; in a build the engine would otherwise come up
            // later (with the in-scene AkInitializer), so those calls fail once and
            // are never retried — e.g. MUS_Scene2_Start posted on frame 1 against an
            // unregistered game object. The editor masks this because the engine
            // already runs in edit mode. Scene AkInitializers detect this instance
            // and self-destruct as duplicates.
            var wwise = new GameObject("WwiseGlobal (Bootstrap)");
            wwise.AddComponent<AkInitializer>();

            Object.DontDestroyOnLoad(Object.Instantiate(Resources.Load("Prefabs/Global")));
        }
    }
}