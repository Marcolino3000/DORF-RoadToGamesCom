using Audio;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class SettingsMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] InGameAudioSettings audioSettings;
        [SerializeField] private Raycaster raycaster;
        
        private Slider masterVolume;
        private Slider dialogVolume;
        private Slider musicVolume;
        private Slider sfxVolume;
        
        private UIDocument uiDocument;
        private VisualElement root;

        public void ToggleMenu()
        {
            if (root.visible)
            {
                HideMenu();
            }
            else
            {
                ShowMenu();
            }
        }
        
        #region Helpers
        private void ShowMenu()
        {
            root.visible = true;
            raycaster.isDialogRunning = true;
        }
        private void HideMenu()
        {
            root.visible = false;
            raycaster.isDialogRunning = false;
        }
        
        #endregion
        
        #region Setup
        
        private void Start()
        {
            GetElements();
            SetupEvents();
            SetSlidersToCurrentValues();
            HideMenu();
        }

        private void GetElements()
        {
            uiDocument = GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
            
            masterVolume = root.Q<Slider>("masterVolume");
            dialogVolume = root.Q<Slider>("dialogVolume");
            musicVolume = root.Q<Slider>("musicVolume");
            sfxVolume = root.Q<Slider>("sfxVolume");
        }

        private void SetupEvents()
        {
            masterVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetMasterVolume(evt.newValue);
                });
            
            dialogVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetDialogVolume(evt.newValue);
                });
            
            musicVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetMusicVolume(evt.newValue);
                });
            
            sfxVolume.RegisterValueChangedCallback(
                evt =>
                {
                    audioSettings.SetSfxVolume(evt.newValue);
                });
        }
        
        private void SetSlidersToCurrentValues()
        {
            masterVolume.value = audioSettings.masterVolume;
            dialogVolume.value = audioSettings.dialogVolume;
            musicVolume.value = audioSettings.musicVolume;
            sfxVolume.value = audioSettings.sfxVolume;
        }
        
        #endregion
    }
}