using System;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public event Action OnStartGame;
    public event Action OnResumeGame;
    public bool IsVisible => root.visible;
    
    private Button startButton;
    private Button resumeButton;
    private Button exitButton;
    
    private UIDocument uiDocument;
    private VisualElement root;
    
    public void Setup()
    {
        SetupElements();
        Show();
    }

    private void SetupElements()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;
        
        startButton = root.Q<Button>("Start");
        resumeButton = root.Q<Button>("Resume");
        exitButton = root.Q<Button>("Exit");
        
        startButton.clicked += StartGame;
        resumeButton.clicked += ResumeGame;
        exitButton.clicked += ExitGame;
    }

    private void StartGame()
    {
        startButton.SetEnabled(false);
        startButton.pickingMode = PickingMode.Ignore;
        OnStartGame?.Invoke();
    }

    private void ResumeGame()
    {
        OnResumeGame?.Invoke();
    }

    private void ExitGame()
    {
        Application.Quit();
    }

    public void Hide()
    {
        root.visible = false;
    }

    public void Show()
    {
        root.visible = true;
    }
}
