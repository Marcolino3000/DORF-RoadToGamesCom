using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class MapMenu : MonoBehaviour
    {
        public bool IsVisible => root.visible;
        
        private UIDocument uiDocument;
        private VisualElement root;

        public void Hide()
        {
            root.visible = false;
        }

        public void Show()
        {
            root.visible = true;
        }

        public void Setup()
        {
            uiDocument = GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
        }

    }
}