using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utility
{
    /// <summary>
    /// Ein Schalter für alle IMGUI-Debug-Overlays. Die vier OnGUI-Blöcke im Spiel
    /// (SceneSetup, DialogBuilderHQ, Sauerteig, TiltCameraMovement) prüfen jeweils ein
    /// eigenes privates [SerializeField] bool namens "showDebugButtons". Drei davon liegen
    /// in Package-Code, der von hier aus nicht änderbar ist — deshalb werden die Felder
    /// per Reflection gesetzt statt über eine gemeinsame statische Property.
    /// OnGUI liest das Feld jeden Frame, das Umschalten wirkt also sofort.
    /// </summary>
    public class DebugGuiSwitch : MonoBehaviour
    {
        private const string FieldName = "showDebugButtons";

        [SerializeField] private bool showDebugButtons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Global.prefab trägt die Komponente nicht, also selbst eine spawnen. Wird sie
            // später doch im Inspector auf Global gelegt, gewinnt diese Instanz und ihre
            // Checkbox bestimmt den Startzustand.
            if (FindAnyObjectByType<DebugGuiSwitch>(FindObjectsInactive.Include) != null)
                return;

            DontDestroyOnLoad(new GameObject(nameof(DebugGuiSwitch), typeof(DebugGuiSwitch)));
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Auf einer Prefab-Instanz läuft Awake mitten im Instantiate von Global, die
        // Geschwister-Komponenten existieren dann evtl. noch nicht. Darum nach jedem
        // Scene-Load erneut anwenden.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.D))
                return;

            // Cmd+D auf macOS, Strg+D auf Windows. Beide Modifier gelten überall, damit im
            // Editor Strg+D funktioniert — Cmd+D ist dort Unitys eigenes "Duplicate".
            if (!Input.GetKey(KeyCode.LeftCommand) && !Input.GetKey(KeyCode.RightCommand) &&
                !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                return;

            showDebugButtons = !showDebugButtons;
            Apply();
        }

        private void Apply()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Null-Einträge kommen von GameObjects mit fehlendem Skript.
                if (behaviour == null || behaviour is DebugGuiSwitch)
                    continue;

                FindField(behaviour.GetType())?.SetValue(behaviour, showDebugButtons);
            }
        }

        private static FieldInfo FindField(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                var field = type.GetField(FieldName, flags);

                if (field != null && field.FieldType == typeof(bool))
                    return field;
            }

            return null;
        }
    }
}
