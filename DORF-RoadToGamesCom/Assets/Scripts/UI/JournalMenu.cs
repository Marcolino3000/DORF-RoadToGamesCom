using System;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

public class JournalMenu : MonoBehaviour
{
    public bool IsVisible => root.visible;
    
    private UIDocument uiDocument;
    private VisualElement root;

    public void Hide()
    {
        // OnMenuToggled?.Invoke(false);
        
        root.visible = false;
    }

    public void Show()
    {
        // OnMenuToggled?.Invoke(true);
        
        root.visible = true;
    }

    public void Setup()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;
        
        // journalMenu = root.Q("journalMenu");
    }

    // private void SetupButtons(VisualElement menu)

    // {

    //     startButton = menu.Q<Button>("Start");

    //     resumeButton = menu.Q<Button>("Resume");

    //     exitButton = menu.Q<Button>("Exit");

    //     

    //     startButton.clicked += StartGame;

    //     resumeButton.clicked += ResumeGame;

    //     exitButton.clicked += ExitGame;

    // }


    // public void UnlockJournal()

    // {

    //     journalIsUnlocked = true;

    //     rightSideContainer.style.display = DisplayStyle.Flex;

    // }

    // public void ToggleMap()
    // {
    //     if (!journalState.ToggleState)
    //         return;
    //     
    //     if(mapIsVisible)
    //     {
    //         mapIsVisible = false;
    //         Hide();
    //     }
    //
    //     else
    //     {
    //         mapIsVisible = true;
    //         ShowMenu();
    //         journalMenu.style.display = DisplayStyle.None;
    //         mapMenu.style.display = DisplayStyle.Flex;   
    //     }
    //
    //     journalIsVisible = false;
    //
    // }
    //
    // public void ToggleJournal()
    // {
    //     if (!journalState.ToggleState)
    //         return;
    //     
    //     if(journalIsVisible)
    //     {
    //         journalIsVisible = false;
    //         Hide();
    //     }
    //
    //     else
    //     {
    //         journalIsVisible = true;
    //         ShowMenu();
    //         journalMenu.style.display = DisplayStyle.Flex;
    //         mapMenu.style.display = DisplayStyle.None;   
    //     }
    //
    //     mapIsVisible = false;
    //
    // }
}