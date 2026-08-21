using System;
using System.Collections;
using Runtime.Scripts.Interactables;
using SceneManagement;
using ScenesSwitches;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

public class JournalMenu : MonoBehaviour
{
    /// <summary>
    /// Raised when the restart hint is clicked, just before the game restarts. MenuToggle listens
    /// and takes the menus down first, the same order the main menu's own RESTART button uses.
    /// </summary>
    public event Action OnRestartGame;

    [Header("Restart Hint")]
    [Tooltip("Der Zettel ist selbst der Button, also muss der Text sagen, wo geklickt wird.")]
    [SerializeField] private string restartHintText = "Click here to restart game";
    [Tooltip("Sekunden, die das Journal fuer sich allein steht, bevor der Zettel auftaucht.")]
    [SerializeField] private float restartHintDelay = 0.6f;
    [SerializeField] private float restartHintFadeDuration = 0.4f;
    [SerializeField] private float restartHintFontSize = 40f;
    [Tooltip("Abstand vom unteren Bildrand in Prozent. Das Buchbild wird auf den ganzen Screen " +
             "gezogen, also trifft ein Prozentwert bei jedem Seitenverhaeltnis dieselbe Stelle der " +
             "Zeichnung — hier die untere Kante des Buchs, unter der das Bild durchsichtig ist.")]
    [SerializeField] private float restartHintBottomPercent = 14f;
    [SerializeField] private Color restartHintColor = new(0.88f, 0.6f, 0.57f);
    [SerializeField] private Color restartHintTextColor = new(0.23f, 0.2f, 0.35f);
    [Tooltip("Nur fuer den Fall, dass auf dem Global-Prefab kein GameResetter liegt: dann wird die " +
             "Szene ohne den Reset der Interactables gewechselt.")]
    [SerializeField] private string firstSceneName = "Scene 1";

    /// <summary>
    /// Tracked here rather than read back from the root element: after the UIDocument rebuilds
    /// its tree the cached root is a detached leftover, and the fresh one is visible because
    /// that is how the UXML authors it. See <see cref="Update"/>.
    /// </summary>
    public bool IsVisible { get; private set; }

    private const string RestartHintName = "restartHint";

    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement restartHint;
    private Coroutine restartHintFade;
    private GameResetter gameResetter;
    private bool restarting;

    public void Hide()
    {
        // OnMenuToggled?.Invoke(false);

        IsVisible = false;
        StopRestartHint();
        if (root != null) root.visible = false;
    }

    public void Show()
    {
        // OnMenuToggled?.Invoke(true);

        IsVisible = true;
        if (root != null) root.visible = true;
        ShowRestartHint();
    }

    public void Setup()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        // journalMenu = root.Q("journalMenu");

        BuildRestartHint();
    }

    /// <summary>
    /// UIDocument throws away its visual tree and builds a new one whenever the source UXML or a
    /// USS it pulls in reimports during Play mode, and whenever the document is disabled and
    /// re-enabled. The cached root is detached at that point, so Hide would write into nothing
    /// while the rebuilt menu sits on screen. Re-acquire it and restore what we last set.
    /// </summary>
    private void Update()
    {
        // Re-fetched rather than trusted: uiDocument is not serialized, so a script recompile in
        // Play mode wipes it and Setup never runs again — the menu would be dead for the session.
        // A disabled UIDocument reports a null root, which must not count as changed.
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var current = uiDocument.rootVisualElement;
        if (current == null || current == root) return;

        var wasVisible = IsVisible;
        Setup();
        if (wasVisible) Show();
        else Hide();
    }

    /// <summary>
    /// The journal is the last page of the demo — a thank-you note — so a visitor who opens it is
    /// done, and the machine has to go back to the start for the next one. The post-it is that
    /// restart button itself, and it is built into the journal's own panel rather than shown on the
    /// HintLogCanvas: the journal covers the screen while it is open, so this is the surface the
    /// click has to land on.
    /// </summary>
    private void BuildRestartHint()
    {
        restartHint = null;

        if (root == null)
            return;

        // Setup runs again on every rebuilt tree, and a second run on the same tree would
        // otherwise stack a second post-it on the first.
        root.Q(RestartHintName)?.RemoveFromHierarchy();

        var button = new Button(RestartGame) { text = restartHintText };

        // Taken from MainMenuStyle.uss, which the journal UXML already pulls in, for the menu font
        // and the nudge on hover. What that class sets beyond it — white 64px on no background —
        // is overridden right here.
        button.AddToClassList("button");
        button.style.fontSize = restartHintFontSize;
        button.style.color = restartHintTextColor;
        button.style.backgroundColor = restartHintColor;
        button.style.whiteSpace = WhiteSpace.NoWrap;
        button.style.paddingLeft = 34;
        button.style.paddingRight = 34;
        button.style.paddingTop = 14;
        button.style.paddingBottom = 18;
        button.style.rotate = new Rotate(new Angle(-2f, AngleUnit.Degree));

        // The row spans the panel so the post-it stays centred at any aspect — the panel scales by
        // width, so nothing here may depend on a fixed height. It is see-through to the pointer, so
        // only the post-it itself takes a click.
        restartHint = new VisualElement { name = RestartHintName, pickingMode = PickingMode.Ignore };
        restartHint.style.position = Position.Absolute;
        restartHint.style.left = 0;
        restartHint.style.right = 0;
        restartHint.style.bottom = Length.Percent(restartHintBottomPercent);
        restartHint.style.alignItems = Align.Center;
        restartHint.style.display = DisplayStyle.None;
        restartHint.Add(button);

        root.Add(restartHint);
    }

    private void ShowRestartHint()
    {
        if (restartHint == null)
            return;

        StopRestartHint();

        // A visitor who opens the journal, backs out and comes back gets the hint again — and with
        // it the click that restarts, which is armed again here rather than staying spent.
        restarting = false;

        restartHintFade = StartCoroutine(FadeInRestartHint());
    }

    /// <summary>
    /// display:none rather than a transparent post-it: an invisible element still answers the
    /// pointer, and while the journal is closed or the delay is still running there is nothing
    /// to click yet.
    /// </summary>
    private void StopRestartHint()
    {
        if (restartHintFade != null)
        {
            StopCoroutine(restartHintFade);
            restartHintFade = null;
        }

        if (restartHint == null)
            return;

        restartHint.style.display = DisplayStyle.None;
        restartHint.style.opacity = 0f;
    }

    private IEnumerator FadeInRestartHint()
    {
        // Unscaled all the way down: getting back to the start screen is the one thing that still
        // has to work if anything left Time.timeScale at zero.
        if (restartHintDelay > 0f)
            yield return new WaitForSecondsRealtime(restartHintDelay);

        restartHint.style.display = DisplayStyle.Flex;

        var elapsed = 0f;

        while (elapsed < restartHintFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            restartHint.style.opacity = Mathf.Clamp01(elapsed / restartHintFadeDuration);
            yield return null;
        }

        restartHint.style.opacity = 1f;
        restartHintFade = null;
    }

    private void RestartGame()
    {
        if (restarting)
            return;

        restarting = true;

        OnRestartGame?.Invoke();

        // The kiosk reset, not just a scene load: it also clears the state the interaction
        // ScriptableObjects carry over from the finished play-through and keeps Marlene from
        // walking while the screen fades. Looked up rather than serialized — GameResetter sits on
        // the Global prefab and this lives in the UI prefab nested inside it.
        if (gameResetter == null)
            gameResetter = FindFirstObjectByType<GameResetter>();

        if (gameResetter != null)
        {
            gameResetter.ResetGame();
            return;
        }

        Debug.LogWarning("JournalMenu: no GameResetter found, restarting without resetting the interactables.", this);
        SceneSwapManager.ChangeScene(firstSceneName);
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