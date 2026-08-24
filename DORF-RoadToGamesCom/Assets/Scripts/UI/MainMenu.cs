using System;
using SceneManagement;
using ScenesSwitches;
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

    /// <summary>
    /// Looked up rather than serialized: GameResetter sits on the Global prefab and this menu lives
    /// in the UI prefab nested inside it.
    /// </summary>
    private GameResetter gameResetter;

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

    /// <summary>
    /// The start screen is StartSplash, and it is already gone by the time this menu can be opened
    /// — so Start here always means "start over", never "leave the start screen". Going back to the
    /// first scene puts the start image up again, so the next visitor picks their language.
    ///
    /// It takes the kiosk reset to get there, not a bare scene load. ChangeScene only swaps the
    /// scene: everything riding on the DontDestroyOnLoad Global prefab stays exactly as the last
    /// play-through left it — Marlene's position, the Sauerteig, the cursor, the Raycaster's input
    /// flags — and the interaction ScriptableObjects keep their counters on top of that. Same lever
    /// JournalMenu's restart hint pulls.
    /// </summary>
    private void StartGame()
    {
        OnStartGame?.Invoke();

        if (gameResetter == null)
            gameResetter = FindFirstObjectByType<GameResetter>();

        if (gameResetter != null)
        {
            gameResetter.ResetGame();
            return;
        }

        Debug.LogWarning("MainMenu: no GameResetter found, restarting without resetting the game state.", this);
        SceneSwapManager.ChangeScene("Scene 1");
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
