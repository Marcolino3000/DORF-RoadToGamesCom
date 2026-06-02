using System;
using Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class SettingsMenu : MonoBehaviour
    {
        public event Action OnResume;
        public bool IsVisible => root.visible;
        
        [Header("References")]
        [SerializeField] InGameAudioSettings audioSettings;
        // [SerializeField] private Raycaster raycaster;
        
        private Slider masterVolume;
        private Slider dialogVolume;
        private Slider musicVolume;
        private Slider sfxVolume;
        private Button resumeButton;
        
        private UIDocument uiDocument;
        private VisualElement root;
        
        
        #region Helpers
        // private void ShowMenu()
        // {
        //     root.visible = true;
        //     // raycaster.isDialogRunning = true;
        // }
        // private void HideMenu()
        // {
        //     root.visible = false;
        //     // raycaster.isDialogRunning = false;
        // }
        
        #endregion
        
        #region Setup
        
        public void Setup()
        {
            GetElements();
            SetupEvents();
            // SetSlidersToCurrentValues();
        }

        private void GetElements()
        {
            uiDocument = GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
            
            masterVolume = root.Q<Slider>("masterVolume");
            dialogVolume = root.Q<Slider>("dialogVolume");
            musicVolume = root.Q<Slider>("musicVolume");
            sfxVolume = root.Q<Slider>("sfxVolume");
            resumeButton = root.Q<Button>("resumeButton");
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
            
            resumeButton.clicked += () => { OnResume?.Invoke(); };
        }
        
        private void SetSlidersToCurrentValues()
        {
            if (audioSettings == null) return;
            
            masterVolume.value = audioSettings.masterVolume;
            dialogVolume.value = audioSettings.dialogVolume;
            musicVolume.value = audioSettings.musicVolume;
            sfxVolume.value = audioSettings.sfxVolume;
        }
        
        #endregion

        public void Hide()
        {
            root.visible = false;
        }

        public void Show()
        {
            root.visible = true;
            SetSlidersToCurrentValues();
        }
    }
}