using Runtime.Scripts.Interactables;
using ScenesSwitches;
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

        /// <summary>
        /// The start image owns the screen, but the toggle keys keep firing behind it — whatever
        /// they opened would be standing on the scene the moment the image fades out.
        /// </summary>
        private static bool MenusLocked => StartSplash.IsShowing;

        private void OnEnable()
        {
            // The start image is also the attract screen: a menu the last visitor walked away from
            // must not still be open when the next one starts.
            StartSplash.OnShown += HideAllMenus;
        }

        private void OnDisable()
        {
            StartSplash.OnShown -= HideAllMenus;
        }

        private void Start()
        {
            Setup();

            // A play-through starts in the game, not in a menu. This lives on the Global prefab, so
            // Start runs while the start image already covers everything — a menu shown here would
            // stay unseen until the image fades and then be sitting on the first scene.
            HideAllMenus();
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
            if (MenusLocked)
                return;

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
            if (MenusLocked)
                return;

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
            if (MenusLocked)
                return;

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
            if (MenusLocked)
                return;

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
