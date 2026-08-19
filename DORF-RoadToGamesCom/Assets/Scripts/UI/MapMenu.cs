using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class MapMenu : MonoBehaviour
    {
        /// <summary>
        /// Tracked here rather than read back from the root element: after the UIDocument rebuilds
        /// its tree the cached root is a detached leftover, and the fresh one is visible because
        /// that is how the UXML authors it. See <see cref="Update"/>.
        /// </summary>
        public bool IsVisible { get; private set; }

        private UIDocument uiDocument;
        private VisualElement root;

        public void Hide()
        {
            IsVisible = false;
            if (root != null) root.visible = false;
        }

        public void Show()
        {
            IsVisible = true;
            if (root != null) root.visible = true;
        }

        public void Setup()
        {
            uiDocument = GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
        }

        /// <summary>
        /// UIDocument throws away its visual tree and builds a new one whenever the source UXML or
        /// a USS it pulls in reimports during Play mode, and whenever the document is disabled and
        /// re-enabled. The cached root is detached at that point, so Hide would write into nothing
        /// while the rebuilt menu sits on screen. Re-acquire it and restore what we last set.
        /// </summary>
        private void Update()
        {
            // Re-fetched rather than trusted: uiDocument is not serialized, so a script recompile
            // in Play mode wipes it and Setup never runs again — the menu would be dead for the
            // session. A disabled UIDocument reports a null root, which must not count as changed.
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var current = uiDocument.rootVisualElement;
            if (current == null || current == root) return;

            var wasVisible = IsVisible;
            Setup();
            if (wasVisible) Show();
            else Hide();
        }
    }
}