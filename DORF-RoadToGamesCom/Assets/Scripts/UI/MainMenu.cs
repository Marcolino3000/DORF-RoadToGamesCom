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

    /// <summary>
    /// Tracked here rather than read back from the root element: after the UIDocument rebuilds
    /// its tree the cached root is a detached leftover, and the fresh one is visible because
    /// that is how the UXML authors it. See <see cref="Update"/>.
    /// </summary>
    public bool IsVisible { get; private set; }

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
    }

    /// <summary>
    /// UIDocument throws away its visual tree and builds a new one whenever the source UXML or a
    /// USS it pulls in reimports during Play mode, and whenever the document is disabled and
    /// re-enabled. Every element cached in SetupElements is detached at that point, so Hide and
    /// the button handlers would write into nothing while the rebuilt menu sits on screen with
    /// no way to close it. Detect it by the root swapping out, then re-acquire and restore.
    /// </summary>
    private void Update()
    {
        // Re-fetched rather than trusted: uiDocument is not serialized, so a script recompile in
        // Play mode wipes it and Setup never runs again — the menu would be dead for the session.
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        // A disabled UIDocument reports a null root. Treating that as "changed" would run Setup
        // against nothing and throw on the first Q<Button>.
        var current = uiDocument.rootVisualElement;
        if (current == null || current == root) return;

        var wasVisible = IsVisible;
        Setup();
        if (wasVisible) Show();
        else Hide();
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
        IsVisible = false;
        if (root != null) root.visible = false;
    }

    public void Show()
    {
        IsVisible = true;
        if (root != null) root.visible = true;
    }
}
