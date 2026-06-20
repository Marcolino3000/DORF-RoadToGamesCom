using Runtime.Scripts.Interactables;
using UnityEngine;

namespace UI
{
    public class MenuToggle : MonoBehaviour
    {
        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private JournalMenu journalMenu;
        [SerializeField] private MapMenu mapMenu;
        [SerializeField] private SettingsMenu settingsMenu;
        
        [SerializeField] private Raycaster raycaster;
        
        private void Start()
        {
            Setup();
            ShowMainMenu();
        }
        
        private void Setup()
        {
            mainMenu.Setup();
            journalMenu.Setup();
            mapMenu.Setup();
            settingsMenu.Setup();

            SetupMenuEvents();
        }

        private void SetupMenuEvents()
        {
            mainMenu.OnStartGame += HideAllMenus;
            mainMenu.OnResumeGame += HideAllMenus;
            mainMenu.OpenSettingsMenu += OpenSettings;
            
            settingsMenu.OnResume += HideAllMenus;
        }

        private void OpenSettings()
        {
            ToggleSettingsMenu();
        }

        public void ToggleMenus()
        {
            if(mainMenu.IsVisible || journalMenu.IsVisible || mapMenu.IsVisible || settingsMenu.IsVisible)
            {
                HideAllMenus();
            }
            
            else
            {
                ShowMainMenu();
            }
        }

        private void HideAllMenus()
        {
            mainMenu.Hide();
            journalMenu.Hide();
            mapMenu.Hide();
            settingsMenu.Hide();
            
            raycaster.IsMenuOpen = false;
        }

        private void ShowMainMenu()
        {
            HideAllMenus();
            mainMenu.Show();
            raycaster.IsMenuOpen = true;
        }

        public void ToggleJournalMenu()
        {
            if (journalMenu.IsVisible)
            {
                HideAllMenus();
                return;
            }
            
            HideAllMenus();
            journalMenu.Show();
            raycaster.IsMenuOpen = true;
        }
        
        public void ToggleMapMenu()
        {
            if(mapMenu.IsVisible)
            {
                HideAllMenus();
                return;
            }
            
            HideAllMenus();
            mapMenu.Show();
            raycaster.IsMenuOpen = true;
        }

        public void ToggleSettingsMenu()
        {
            if (settingsMenu.IsVisible)
            {
                HideAllMenus();
                return;
            }
            
            HideAllMenus();
            settingsMenu.Show();
            raycaster.IsMenuOpen = true;   
        }
    }
    
    
    
}