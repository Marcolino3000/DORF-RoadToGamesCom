using Runtime.Scripts.Interactables;
using UnityEngine;

namespace UI
{
    public class MenuToggle : MonoBehaviour
    {
        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private JournalMenu journalMenu;
        [SerializeField] private MapMenu mapMenu;
        
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

            SetupMainMenuEvents();
        }

        private void SetupMainMenuEvents()
        {
            mainMenu.OnStartGame += HideAllMenus;
            mainMenu.OnResumeGame += HideAllMenus;
        }

        public void ToggleMenus()
        {
            if(mainMenu.IsVisible || journalMenu.IsVisible || mapMenu.IsVisible)
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
            
            raycaster.isDialogRunning = false;
        }

        private void ShowMainMenu()
        {
            HideAllMenus();
            mainMenu.Show();
            raycaster.isDialogRunning = true;
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
            raycaster.isDialogRunning = true;
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
            raycaster.isDialogRunning = true;
        }
    }
    
    
    
}