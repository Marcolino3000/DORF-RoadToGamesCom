using System;
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
        /// Raised when a menu comes up and when the last one goes away. Static because this lives
        /// on the Global prefab and MusicDirector wants to listen without either knowing the other.
        /// Switching straight from one menu to another does not raise — see <see cref="SetMenusOpen"/>.
        /// </summary>
        public static event Action<bool> OnMenuOpenStateChanged;

        private bool menusOpen;

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

            // The journal's restart post-it: menus down first, then the restart it triggers itself.
            journalMenu.OnRestartGame += HideAllMenus;

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
            HideAllPanels();
            SetMenusOpen(false);
        }

        /// <summary>
        /// Just the panels. The open state stays as it is, so the show paths below can clear the
        /// screen before putting their own menu up without the state flicking closed in between.
        /// </summary>
        private void HideAllPanels()
        {
            mainMenu.Hide();
            journalMenu.Hide();
            mapMenu.Hide();
            settingsMenu.Hide();
        }

        /// <summary>
        /// Only on the actual change. Every show path hides the others first, so without this a
        /// visitor tabbing from the journal to the map would close and reopen the menus as far as
        /// anyone listening is concerned — and the menu music would restart on each switch.
        /// </summary>
        private void SetMenusOpen(bool open)
        {
            raycaster.IsMenuOpen = open;

            if (menusOpen == open)
                return;

            menusOpen = open;
            OnMenuOpenStateChanged?.Invoke(open);
        }

        private void ShowMainMenu()
        {
            HideAllPanels();
            mainMenu.Show();
            SetMenusOpen(true);
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
            
            HideAllPanels();
            journalMenu.Show();
            SetMenusOpen(true);
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
            
            HideAllPanels();
            mapMenu.Show();
            SetMenusOpen(true);
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
            
            HideAllPanels();
            settingsMenu.Show();
            SetMenusOpen(true);
        }
    }
    
    
    
}
