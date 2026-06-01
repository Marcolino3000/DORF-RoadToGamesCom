using System;
using SceneManagement;
using UI;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public event Action OnStartGame;
    public event Action OnResumeGame;
    public event Action OpenSettingsMenu;
    public bool IsVisible => root.visible;
    
    private Button startButton;
    private Button resumeButton;
    private Button exitButton;
    private Button settingsButton;
    
    private UIDocument uiDocument;
    private VisualElement root;
    private bool gameStarted;
    
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
        settingsButton = root.Q<Button>("Settings");
        
        startButton.clicked += StartGame;
        resumeButton.clicked += ResumeGame;
        exitButton.clicked += ExitGame;
        settingsButton.clicked += OpenSettings;
    }

    private void OpenSettings()
    {
        OpenSettingsMenu?.Invoke();
    }

    private void StartGame()
    {
        if(!gameStarted)
        {
            gameStarted = true;
            OnStartGame?.Invoke();
        }

        else
        {
            gameStarted = false;
            SceneSwapManager.ChangeScene("Scene 1");
        }
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
