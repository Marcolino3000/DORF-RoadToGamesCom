using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Audio;
using Nodes;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class Smartphone : MonoBehaviour, ISceneSetupCallbackReceiver
    {
        /// <summary>
        /// Raised once per scene, when a run of consecutive voice memos has played to its end.
        /// <see cref="ScenesSwitches.SceneTransitionManager"/> hangs the title sequence off this.
        /// </summary>
        public event Action OnVoiceChainFinished;

        /// <summary>
        /// Raised whenever the phone opens (true) or closes (false), no matter what triggered it —
        /// the menu button, a click next to the phone, or the reset closing it in OnSceneSetup.
        /// Static because the phone lives in the scene and MusicDirector on the persistent Global
        /// prefab cannot be handed a reference to it.
        /// </summary>
        public static event Action<bool> OnOpenStateChanged;

        [SerializeField] private Raycaster raycaster;

        [Header("Status Bar")]
        [SerializeField] private string time = "9:41";
        [SerializeField] private string cellularLabel = "3G";

        [Header("Chats")]
        [SerializeField] private TextAsset contactsJson;
        [SerializeField] private VisualTreeAsset chatCardTemplate;
        [SerializeField] private VisualTreeAsset messageBubbleTemplate;

        [Header("Profile Pictures")]
        [Tooltip("Maps the avatarId of a contact in Contacts.json to their picture.")]
        [SerializeField] private List<ContactAvatar> contactAvatars = new();
        [Tooltip("Marlene's own picture, shown on the Profile tab of the nav bar.")]
        [SerializeField] private Texture2D selfAvatar;

        [Header("Voice Memos")]
        [Tooltip("Maps the voiceId of a \"voice\" message in Contacts.json to the clip it plays.")]
        [SerializeField] private List<VoiceMemoEntry> voiceMemos = new();
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private InGameAudioSettings audioSettings;

        private UIDocument uiDocument;
        private VisualElement root;
        private Label timeLabelEl;
        private Label cellularLabelEl;
        private ListView chatListView;

        // Phone shell. phoneScreen sits at fixed offsets inside phoneContainer
        // and the bezel PNG is scale-to-fit, so the two only line up while the
        // container keeps exactly these proportions.
        private const float PhoneDesignWidth = 560f;
        private const float PhoneDesignHeight = 1038f;
        private VisualElement phoneRoot;
        private VisualElement phoneContainer;
        private float appliedPhoneScale = -1f;

        // Conversation page elements.
        private VisualElement chatsPage;
        private VisualElement chatPage;
        private VisualElement navBar;
        private Button backButton;
        private Label chatViewName;
        private VisualElement chatViewAvatar;
        private ListView messagesListView;

        // Untertitelleiste. Sie hängt in smartphoneRoot und ist damit zusammen mit dem Handy
        // weg — eine Sprachnachricht läuft bei geschlossenem Handy zwar weiter, aber dann ist
        // die Nachricht selbst samt Wellenform ebenso wenig zu sehen.
        private VisualElement subtitleBar;
        private Label subtitleTextEl;

        private readonly List<Contact> contacts = new();
        private readonly Dictionary<string, VoiceMemoEntry> memosByVoiceId = new();
        private readonly Dictionary<string, Texture2D> avatarsById = new();
        private Contact currentContact;
        private bool isOpen;

        // The memo that is loaded — playing or paused. Null means nothing is loaded.
        private ContactMessage playingMessage;
        private bool isPaused;
        // The realised row of the playing memo, so the progress fill can be updated without a
        // rebind. ListView recycles rows, so bindItem is what keeps this honest.
        private MessageBubbleRefs playingRefs;
        private bool voiceChainFinished;
        private int voiceSpeedIndex;
        // Memos that have played all the way through, so a run can tell whether it was fully heard.
        private readonly HashSet<ContactMessage> playedInRun = new();
        // Stops the stretch workers when the scene goes away, instead of letting them finish work
        // nobody will read — on a kiosk that is every inactivity reset.
        private readonly CancellationTokenSource stretchCancellation = new();

        // Which of the entry's takes the AudioSource currently holds. Each take is a different
        // length, so the playhead only travels between them as a fraction.
        private int loadedTakeIndex;

        // Der Cue, der gerade auf der Leiste steht. Gemerkt, damit der Label-Text nur beim
        // Wechsel neu gesetzt wird statt in jedem Frame — ein Label neu zu betexten macht das
        // Layout schmutzig, und die Leiste steht bei der längsten Nachricht über eine Minute.
        // -1 heißt: nichts angezeigt.
        private ContactMessage shownCueMessage;
        private int shownCueIndex = -1;

        private void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            BuildAvatarLookup();
            BuildClipLookup();
            LoadContacts();
            BindRoot(initial: true);

            if (audioSettings != null)
                audioSettings.OnDialogVolumeChanged += SetVoiceVolume;

            StartCoroutine(BuildStretchedTakes());
        }

        /// <summary>
        /// Derives the 1.5x and 2x takes from every memo clip, once, off the main thread. Measured
        /// at roughly 250 ms for the longest memo and under 600 ms for all four takes together, so
        /// spreading them over the cores puts the whole job well inside the train intro that runs
        /// before the phone can even be opened. Until a take is ready the pill falls back to the
        /// original clip, so nothing breaks if a visitor is unusually quick.
        /// </summary>
        private IEnumerator BuildStretchedTakes()
        {
            var pending = new List<(VoiceMemoEntry entry, float[] samples, int frequency)>();

            foreach (var entry in memosByVoiceId.Values)
            {
                var clip = entry.clip;
                if (clip == null) continue;

                // Loaded first, and for every memo: a clip left unloaded here would not be resident
                // when the visitor taps it, and Play() on unloaded data does not reliably report
                // isPlaying on the same frame — which UpdateVoicePlayback would read as "finished".
                // The dialog clips are imported without Preload Audio Data, so this is the norm.
                if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
                {
                    Debug.LogWarning($"[Smartphone] Could not load '{clip.name}', so '{entry.voiceId}' stays at 1x.", this);
                    continue;
                }

                if (clip.channels != 1)
                {
                    Debug.LogWarning($"[Smartphone] '{clip.name}' is not mono, so '{entry.voiceId}' stays at 1x.", this);
                    continue;
                }

                var samples = new float[clip.samples];
                if (!clip.GetData(samples, 0))
                {
                    Debug.LogWarning($"[Smartphone] Could not read '{clip.name}', so '{entry.voiceId}' stays at 1x.", this);
                    continue;
                }

                pending.Add((entry, samples, clip.frequency));
            }

            if (pending.Count == 0) yield break;

            // One job per memo and rate. Pre-allocated so every job writes to its own slot and the
            // parallel loop needs no locking.
            var stretched = new float[pending.Count][][];
            var jobs = new List<(int source, int speed)>();
            for (var i = 0; i < pending.Count; i++)
            {
                stretched[i] = new float[VoiceSpeeds.Length][];
                for (var s = 1; s < VoiceSpeeds.Length; s++) jobs.Add((i, s));
            }

            var token = stretchCancellation.Token;
            var work = Task.Run(() => Parallel.ForEach(
                jobs,
                new ParallelOptions { CancellationToken = token },
                job => stretched[job.source][job.speed] =
                    WsolaTimeStretch.Stretch(pending[job.source].samples, VoiceSpeeds[job.speed])), token);

            // Logged from a continuation as well: if the scene is reloaded mid-run the coroutine
            // dies here and nothing would ever read work.Exception, so a fault in the stretcher
            // would pass the whole exhibition in silence. No context object — this is off-thread.
            work.ContinueWith(t => Debug.LogError($"[Smartphone] Time stretching faulted: {t.Exception?.Flatten()}"),
                TaskContinuationOptions.OnlyOnFaulted);

            while (!work.IsCompleted) yield return null;

            if (work.IsFaulted || work.IsCanceled) yield break;

            // AudioClip.Create and SetData are main-thread only, which is why the worker only ever
            // hands back plain float arrays.
            for (var i = 0; i < pending.Count; i++)
            {
                var (entry, _, frequency) = pending[i];
                var takes = new AudioClip[VoiceSpeeds.Length];
                takes[0] = entry.clip;

                for (var s = 1; s < VoiceSpeeds.Length; s++)
                {
                    var data = stretched[i][s];
                    // AudioClip.Create throws on a zero length, which would abort the loop and leave
                    // every memo after this one without its takes.
                    if (data == null || data.Length == 0) continue;

                    var built = AudioClip.Create($"{entry.clip.name}_{VoiceSpeeds[s]:0.#}x", data.Length, 1, frequency, false);
                    built.SetData(data, 0);
                    takes[s] = built;
                }

                entry.Takes = takes;
            }
        }

        private void OnDestroy()
        {
            // InGameAudioSettings is a ScriptableObject and outlives the scene — a handler left
            // behind here would fire into a destroyed component after the kiosk reset.
            if (audioSettings != null)
                audioSettings.OnDialogVolumeChanged -= SetVoiceVolume;

            // The Raycaster sits on the DontDestroyOnLoad Global prefab, so a block we took out
            // while the phone was open survives us. A visitor who walks away with the phone open
            // would otherwise hand the next one a scene where nothing can be clicked.
            if (isOpen) SetWorldInputBlocked(false);

            stretchCancellation.Cancel();
            stretchCancellation.Dispose();
        }

        /// <summary>
        /// Runs on every scene load and on the inactivity reset (SceneSetup finds this through
        /// FindObjectsByType, so it needs no wiring). Without it a memo started just before the
        /// timeout keeps talking under the attract video and then fires the title sequence into
        /// the reset that is already loading Scene 1.
        /// </summary>
        public void OnSceneSetup()
        {
            StopVoice();
            voiceChainFinished = false;
            playedInRun.Clear();
            if (isOpen) Close();
        }

        private void Update()
        {
            // UI Toolkit Live Reload rebuilds the panel tree whenever the
            // source UXML/USS reimports during Play mode. The cached refs
            // become stale and the new ListView has no makeItem/bindItem.
            // Detect via reference change and re-bind.
            if (uiDocument == null) return;
            var current = uiDocument.rootVisualElement;
            if (current != null && current != root)
            {
                BindRoot(initial: false);
            }

            UpdateVoicePlayback();
        }

        private void BindRoot(bool initial)
        {
            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                if (initial)
                    Debug.LogError("Smartphone: UIDocument.rootVisualElement is null. Is the source asset assigned?", this);
                return;
            }

            timeLabelEl = root.Q<Label>("timeLabel");
            cellularLabelEl = root.Q<Label>("cellularLabel");
            chatListView = root.Q<ListView>("chatListView");

            if (phoneRoot != null)
            {
                phoneRoot.UnregisterCallback<GeometryChangedEvent>(OnPhoneRootGeometryChanged);
                phoneRoot.UnregisterCallback<ClickEvent>(OnBackdropClicked);
            }
            phoneRoot = root.Q<VisualElement>("smartphoneRoot");
            phoneContainer = root.Q<VisualElement>("phoneContainer");
            appliedPhoneScale = -1f;
            if (phoneRoot != null)
            {
                phoneRoot.RegisterCallback<GeometryChangedEvent>(OnPhoneRootGeometryChanged);
                phoneRoot.RegisterCallback<ClickEvent>(OnBackdropClicked);
                FitPhoneToPanel();
            }

            chatsPage = root.Q<VisualElement>("chatsPage");
            chatPage = root.Q<VisualElement>("chatPage");
            navBar = root.Q<VisualElement>("navBar");
            backButton = root.Q<Button>("backButton");
            chatViewName = root.Q<Label>("chatViewName");
            chatViewAvatar = root.Q<VisualElement>("chatViewAvatar");
            messagesListView = root.Q<ListView>("messagesListView");

            // Frisch aus dem Baum geholt, also unbetextet und unsichtbar — der Merker muss mit.
            // Läuft gerade eine Nachricht, setzt UpdateSubtitle die Zeile im nächsten Frame
            // wieder auf.
            subtitleBar = root.Q<VisualElement>("subtitleBar");
            subtitleTextEl = root.Q<Label>("subtitleText");
            HideSubtitle();

            // Marlene's own picture on the Profile tab never changes, so it is set once per bind.
            ApplyAvatar(root.Q<VisualElement>("profileIcon"), selfAvatar);

            if (backButton != null)
            {
                backButton.clicked -= CloseChat;
                backButton.clicked += CloseChat;
            }

            ApplyStatusBar();
            ApplyChromeLanguage();
            BindChatList();

            // After a hot reload, restore the conversation if one was open.
            if (currentContact != null)
            {
                ShowChatPage(currentContact);
            }

            SetVisible(initial ? false : isOpen);
        }

        /// <summary>Time the status bar shows, as it is written there ("10:40").</summary>
        public string StatusBarTime => time;

        public void SetTime(string value)
        {
            time = value;
            if (timeLabelEl != null) timeLabelEl.text = value;
        }

        public void SetCellular(string value)
        {
            cellularLabel = value;
            if (cellularLabelEl != null) cellularLabelEl.text = value;
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            SetVisible(true);
            SetWorldInputBlocked(true);
            // BindRoot ran in Start, while the start screen was still up and the visitor had not
            // picked a language yet. Opening the phone is always later than that, so the language
            // is settled by now and both the chrome and the bound rows are redone here.
            ApplyChromeLanguage();
            // ListView in a hidden panel has a 0x0 viewport and won't
            // create rows. Refresh once visible so cards appear.
            chatListView?.RefreshItems();
        }

        public void Close()
        {
            SetVisible(false);
            SetWorldInputBlocked(false);
        }

        /// <summary>
        /// Closes the phone when the click landed next to it, the way tapping outside a modal does.
        /// ClickEvent bubbles, so clicks from inside the phone arrive here too — anything within
        /// phoneContainer is left alone, bezel included, since the overlay above it does not pick
        /// and the container is what the shell's own pixels resolve to.
        ///
        /// On the click rather than on the press: the Raycaster reads the world click on press,
        /// while IsMenuOpen still blocks it, so the click that closes the phone cannot also send
        /// Marlene walking. Never closes a chat instead — the back button is what backs out of one.
        /// </summary>
        private void OnBackdropClicked(ClickEvent evt)
        {
            if (!isOpen) return;

            // Walked by hand rather than through VisualElement.Contains: a click on the bezel
            // targets phoneContainer itself, and the element must count as its own ancestor here.
            for (var element = evt.target as VisualElement; element != null; element = element.hierarchy.parent)
                if (element == phoneContainer) return;

            Close();
        }

        /// <summary>
        /// The phone is a modal overlay, so the world behind it must not react to the pointer.
        /// IsMenuOpen is the flag the Raycaster honours unconditionally — isDialogRunning only
        /// bites when its disableMouseInputDuringDialog setting is on, and that is off on Global.
        /// </summary>
        private void SetWorldInputBlocked(bool blocked)
        {
            // The Raycaster lives on the DontDestroyOnLoad Global prefab, so it cannot be dragged
            // onto a scene component in the Inspector. Same lookup AwarenessHoverParticles uses.
            if (raycaster == null)
                raycaster = FindFirstObjectByType<Raycaster>();

            if (raycaster == null) return;
            raycaster.IsMenuOpen = blocked;
        }

        private void ApplyStatusBar()
        {
            if (timeLabelEl != null) timeLabelEl.text = time;
            if (cellularLabelEl != null) cellularLabelEl.text = cellularLabel;
        }

        /// <summary>
        /// The labels the phone owns itself rather than reading from Contacts.json — headline,
        /// search placeholder and nav bar. They sit in the UXML in German, so both languages are
        /// written out here: the German branch has to restore them when a visitor picks German
        /// after one who picked English.
        /// </summary>
        private void ApplyChromeLanguage()
        {
            if (root == null) return;

            var english = IsEnglish;

            SetLabel("chatsHeadline", "Chats");
            SetLabel("searchPlaceholder", english ? "Search" : "Suche");
            SetLabel("navCallsLabel", english ? "CALLS" : "ANRUFE");
            SetLabel("navChatsLabel", "CHATS");
            SetLabel("navProfileLabel", english ? "PROFILE" : "PROFIL");

            // Nach einem Sprachwechsel steht die laufende Zeile noch in der alten Sprache da.
            // Der zurückgesetzte Merker zwingt UpdateSubtitle, sie neu zu setzen.
            shownCueMessage = null;
            shownCueIndex = -1;
        }

        private void SetLabel(string elementName, string value)
        {
            var label = root.Q<Label>(elementName);
            if (label != null) label.text = value;
        }

        private void SetVisible(bool visible)
        {
            // Only on the actual change: BindRoot re-applies the current value after a UI Toolkit
            // hot reload, and that must not read as the visitor opening the phone again.
            var changed = isOpen != visible;

            isOpen = visible;

            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (changed)
                OnOpenStateChanged?.Invoke(visible);
        }

        private void OnPhoneRootGeometryChanged(GeometryChangedEvent _) => FitPhoneToPanel();

        // "Menu PanelSettings" scales with screen WIDTH (match = 0), so the panel's
        // logical height is 1920 / aspect: 1080 at 16:9, only ~804 on a 21:9
        // ultrawide. The phone needs 1038 of that — flex would squash the container,
        // the scale-to-fit bezel then letterboxes inside the squashed box while
        // phoneScreen keeps its fixed pixel offsets, and the screen slides out from
        // under the bezel. phoneContainer is flex-shrink: 0 so its box always stays
        // 560x1038; scale the whole phone uniformly instead, which keeps the design
        // pixel-exact including the text inside the screen.
        private void FitPhoneToPanel()
        {
            if (phoneRoot == null || phoneContainer == null) return;

            var available = phoneRoot.contentRect;
            if (available.width <= 0f || available.height <= 0f) return;

            // Never larger than designed — at 16:9 this leaves the phone untouched.
            const float margin = 0.98f;
            var scale = Mathf.Min(available.width * margin / PhoneDesignWidth,
                                  available.height * margin / PhoneDesignHeight,
                                  1f);
            if (Mathf.Approximately(scale, appliedPhoneScale)) return;
            appliedPhoneScale = scale;
            phoneContainer.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        }

        private void BuildAvatarLookup()
        {
            avatarsById.Clear();
            foreach (var entry in contactAvatars)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.avatarId)) continue;
                if (entry.picture == null)
                {
                    Debug.LogWarning($"[Smartphone] Avatar '{entry.avatarId}' has no picture assigned.", this);
                    continue;
                }
                avatarsById[entry.avatarId] = entry.picture;
            }
        }

        /// <summary>
        /// The contact's picture, or null — a contact without one keeps the plain grey circle the
        /// stylesheet draws underneath, which is a fine stand-in rather than a hole in the UI.
        /// </summary>
        private Texture2D AvatarFor(Contact contact)
        {
            if (contact?.avatarId == null) return null;
            return avatarsById.TryGetValue(contact.avatarId, out var picture) ? picture : null;
        }

        private static void ApplyAvatar(VisualElement element, Texture2D picture)
        {
            if (element == null) return;
            element.style.backgroundImage = picture != null
                ? new StyleBackground(picture)
                : new StyleBackground(StyleKeyword.None);
        }

        private void BuildClipLookup()
        {
            memosByVoiceId.Clear();
            foreach (var entry in voiceMemos)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.voiceId)) continue;
                if (entry.clip == null)
                {
                    Debug.LogWarning($"[Smartphone] Voice memo '{entry.voiceId}' has no clip assigned.", this);
                    continue;
                }

                memosByVoiceId[entry.voiceId] = entry;
            }
        }

        private void LoadContacts()
        {
            contacts.Clear();
            if (contactsJson == null)
            {
                Debug.LogWarning("[Smartphone] 'Contacts Json' field is not assigned. Drag Assets/UI/Smartphone/Contacts.json onto it in the Inspector.", this);
                return;
            }
            var parsed = JsonUtility.FromJson<ContactsRoot>(contactsJson.text);
            if (parsed?.contacts == null || parsed.contacts.Count == 0)
            {
                Debug.LogWarning($"[Smartphone] JSON parsed but produced 0 contacts (text length = {contactsJson.text.Length}). Check field names match (contact, messages, text, timestamp, sender, status).", this);
                return;
            }
            contacts.AddRange(parsed.contacts);
            WarnAboutUnresolvedVoiceMemos();
            Debug.Log($"[Smartphone] Loaded {contacts.Count} contacts.", this);
        }

        /// <summary>
        /// A memo whose voiceId has no clip would silently do nothing when tapped — and if it is
        /// the one the title sequence hangs off, the play-through stalls. Say so on load instead.
        /// </summary>
        private void WarnAboutUnresolvedVoiceMemos()
        {
            foreach (var contact in contacts)
            {
                if (contact.messages == null) continue;
                foreach (var message in contact.messages)
                {
                    if (!message.IsVoice) continue;
                    if (ResolveEntry(message) == null)
                        Debug.LogWarning($"[Smartphone] Voice memo in '{contact.contact}' at {message.timestamp} has voiceId '{message.voiceId}', which is not in Voice Memos.", this);
                }
            }
        }

        private void BindChatList()
        {
            if (chatListView == null)
            {
                Debug.LogWarning("[Smartphone] No element named 'chatListView' found in the UXML.", this);
                return;
            }
            if (chatCardTemplate == null)
            {
                Debug.LogWarning("[Smartphone] 'Chat Card Template' field is not assigned. Drag Assets/UI/Smartphone/ChatCard.uxml onto it in the Inspector.", this);
                return;
            }

            // Set makers BEFORE itemsSource — assigning itemsSource is the
            // trigger that begins binding, and it needs makeItem ready.
            chatListView.makeItem = () =>
            {
                var card = chatCardTemplate.Instantiate();
                var refs = new ChatCardRefs
                {
                    Name = card.Q<Label>("cardName"),
                    Preview = card.Q<Label>("cardPreview"),
                    Time = card.Q<Label>("cardTime"),
                    Badge = card.Q<VisualElement>("cardBadge"),
                    BadgeLabel = card.Q<Label>("cardBadgeLabel"),
                    StatusIcon = card.Q<VisualElement>("statusIcon"),
                    Avatar = card.Q<VisualElement>("avatar"),
                };
                card.userData = refs;
                // Direct click handler — bypasses ListView.selection, which
                // re-fires on visibility changes and would re-open the chat
                // immediately after the back button is pressed.
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (refs.CurrentContact != null) ShowChatPage(refs.CurrentContact);
                });
                return card;
            };

            chatListView.bindItem = (element, index) =>
            {
                var refs = element.userData as ChatCardRefs;
                if (refs == null) return;
                var c = contacts[index];
                refs.CurrentContact = c;

                refs.Name.text = c.LocalizedName;
                ApplyAvatar(refs.Avatar, AvatarFor(c));

                var (preview, timeStr) = LastMessage(c);
                refs.Preview.text = preview;
                refs.Time.text = timeStr;

                var unread = CountUnread(c);
                if (unread > 0)
                {
                    refs.Badge.style.display = DisplayStyle.Flex;
                    refs.BadgeLabel.text = unread.ToString();
                }
                else
                {
                    refs.Badge.style.display = DisplayStyle.None;
                }

                // Status icon next to the message preview reflects the LAST
                // message's read receipt — but only when that message was
                // sent by Marlene. Received last messages show no icon.
                if (refs.StatusIcon != null)
                {
                    foreach (var m in CardStatusModifiers) refs.StatusIcon.RemoveFromClassList(m);
                    var last = (c.messages != null && c.messages.Count > 0) ? c.messages[c.messages.Count - 1] : null;
                    if (last != null && last.sender == "me" && !string.IsNullOrEmpty(last.status))
                    {
                        refs.StatusIcon.AddToClassList("chat-card__status-icon--" + last.status);
                    }
                }
            };

            chatListView.itemsSource = contacts;
            chatListView.Rebuild();
            Debug.Log($"[Smartphone] Chat list bound with {contacts.Count} rows.", this);
        }

        #region Voice memo playback

        private const string VoicePlayingClass = "voice-memo__play--playing";
        private const string VoiceBarPlayedClass = "voice-memo__bar--played";

        /// <summary>
        /// The rates the speed pill cycles through. Index 0 is the recording as it was spoken; the
        /// rest are WSOLA takes derived from it, which is what keeps the voice where it belongs.
        /// </summary>
        private static readonly float[] VoiceSpeeds = { 1f, 1.5f, 2f };

        private VoiceMemoEntry ResolveEntry(ContactMessage message)
        {
            if (message?.voiceId == null) return null;
            return memosByVoiceId.TryGetValue(message.voiceId, out var entry) ? entry : null;
        }

        /// <summary>The memo's length as spoken, which is what the caption counts against.</summary>
        private static float DurationOf(VoiceMemoEntry entry) => entry?.clip != null ? entry.clip.length : 0f;

        /// <summary>
        /// The take for a rate, falling back to the original whenever the stretched one is not there
        /// — still being built, or a clip that could not be read. Playing slower than asked beats
        /// playing at the wrong pitch.
        /// </summary>
        private static AudioClip TakeFor(VoiceMemoEntry entry, int speedIndex)
        {
            if (entry == null) return null;
            if (speedIndex == 0 || entry.Takes == null) return entry.clip;
            return entry.Takes[speedIndex] != null ? entry.Takes[speedIndex] : entry.clip;
        }

        /// <summary>Index of the take that is actually available for the current rate.</summary>
        private int AvailableTakeIndex(VoiceMemoEntry entry)
        {
            if (entry?.Takes == null) return 0;
            return entry.Takes[voiceSpeedIndex] != null ? voiceSpeedIndex : 0;
        }

        private void ToggleVoicePlayback(MessageBubbleRefs refs)
        {
            var message = refs?.CurrentMessage;
            if (message == null || !message.IsVoice) return;

            // Tapping the memo that is currently running pauses it — and with it the autoplay
            // chain, because only a memo that reaches its own end hands over to the next one.
            if (message == playingMessage && !isPaused)
            {
                PauseVoice();
                return;
            }

            PlayVoice(message);
        }

        private void PlayVoice(ContactMessage message)
        {
            var entry = ResolveEntry(message);
            if (entry == null)
            {
                Debug.LogWarning($"[Smartphone] Nothing wired for voiceId '{message.voiceId}'.", this);
                return;
            }

            if (voiceAudioSource == null)
            {
                Debug.LogWarning("[Smartphone] 'Voice Audio Source' is not assigned, no memo can play.", this);
                return;
            }

            if (message == playingMessage && isPaused)
            {
                voiceAudioSource.UnPause();
            }
            else
            {
                // Stop first: swapping the clip on a source that is merely paused leaves Play() to
                // resume at the old offset, which may already be past the new clip's end.
                voiceAudioSource.Stop();

                // The rate carries into the next memo of an autoplay chain, the way a messenger keeps
                // the setting for the conversation rather than resetting it per message.
                loadedTakeIndex = AvailableTakeIndex(entry);
                voiceAudioSource.clip = TakeFor(entry, loadedTakeIndex);
                voiceAudioSource.volume = audioSettings != null ? audioSettings.GetDialogVolume() : 1f;
                voiceAudioSource.Play();
                playingMessage = message;
            }

            isPaused = false;
            messagesListView?.RefreshItems();
        }

        /// <summary>
        /// Steps the pill through 1x / 1.5x / 2x. Only the running memo shows one, so the rate always
        /// applies to what the visitor is hearing; it then carries over to the rest of the chain.
        /// </summary>
        private void CycleVoiceSpeed(MessageBubbleRefs refs)
        {
            var message = refs?.CurrentMessage;
            if (message == null || message != playingMessage || voiceAudioSource == null) return;

            voiceSpeedIndex = (voiceSpeedIndex + 1) % VoiceSpeeds.Length;

            SyncLoadedTake();

            // Captioned after the swap, and from the take that is actually loaded — while the
            // stretched takes are still being built the pill would otherwise promise 2x over a
            // memo running at 1x. No RefreshItems: that would rebuild the bars and drop the
            // progress fill for a frame.
            if (refs.VoiceSpeedButton != null)
                refs.VoiceSpeedButton.text = FormatSpeed(VoiceSpeeds[loadedTakeIndex]);
        }

        /// <summary>
        /// Puts the take matching the picked rate on the AudioSource, carrying the playhead over.
        /// Called on every frame as well as on a tap, because a memo started before the stretched
        /// takes had finished building runs at 1x and would otherwise stay there for good.
        /// </summary>
        private void SyncLoadedTake()
        {
            if (playingMessage == null || voiceAudioSource == null) return;

            var entry = ResolveEntry(playingMessage);
            var wanted = AvailableTakeIndex(entry);
            if (wanted == loadedTakeIndex) return;

            var take = TakeFor(entry, wanted);
            if (take == null) return;

            // Each take is a different length, so the playhead crosses over as a fraction rather
            // than as seconds.
            var current = voiceAudioSource.clip;
            var progress = current != null && current.length > 0f
                ? Mathf.Clamp01(voiceAudioSource.time / current.length)
                : 0f;

            var wasPaused = isPaused;
            loadedTakeIndex = wanted;
            voiceAudioSource.clip = take;
            voiceAudioSource.Play();
            // Pause before seeking when it was paused: the audio thread can mix a buffer between
            // Play() and the seek, which would blip the memo's opening while it should stay silent.
            // The position sticks on a paused source; it is only a stopped one that discards it.
            if (wasPaused) voiceAudioSource.Pause();
            voiceAudioSource.time = Mathf.Min(progress * take.length, Mathf.Max(0f, take.length - 0.01f));
        }

        /// <summary>
        /// "1x" / "1,5x" / "2x", with the decimal separator of the picked language. Plain "x" rather
        /// than the multiplication sign, which the phone's Fancy-Regular may not carry a glyph for.
        /// </summary>
        private static string FormatSpeed(float speed)
        {
            var text = speed.ToString("0.#", CultureInfo.InvariantCulture);
            if (!IsEnglish) text = text.Replace('.', ',');
            return text + "x";
        }

        /// <summary>
        /// Drops playback and forgets the memo, without going through <see cref="HandleVoiceFinished"/>
        /// — abandoning a memo is not the same as hearing it out, and must never advance the chain
        /// or fire the title sequence.
        /// </summary>
        private void StopVoice()
        {
            if (voiceAudioSource != null) voiceAudioSource.Stop();
            playingMessage = null;
            playingRefs = null;
            isPaused = false;
            loadedTakeIndex = 0;
            HideSubtitle();
        }

        private void PauseVoice()
        {
            if (playingMessage == null || voiceAudioSource == null) return;
            voiceAudioSource.Pause();
            isPaused = true;
            messagesListView?.RefreshItems();
        }

        private void SetVoiceVolume(float volume)
        {
            if (voiceAudioSource != null) voiceAudioSource.volume = volume;
        }

        private void UpdateVoicePlayback()
        {
            if (playingMessage == null || isPaused || voiceAudioSource == null) return;

            if (!voiceAudioSource.isPlaying)
            {
                HandleVoiceFinished();
                return;
            }

            SyncLoadedTake();
            UpdateVoiceProgress();
            UpdateSubtitle();
        }

        /// <summary>
        /// A memo reached its end. WhatsApp keeps going as long as the next message is another memo
        /// from the same sender, and stops at the first thing that is not.
        /// </summary>
        private void HandleVoiceFinished()
        {
            var finished = playingMessage;
            playingMessage = null;
            playingRefs = null;
            playedInRun.Add(finished);

            var next = NextMessage(finished);
            if (next != null && next.IsVoice && next.sender == finished.sender && ResolveEntry(next) != null)
            {
                PlayVoice(next);
                return;
            }

            HideSubtitle();
            messagesListView?.RefreshItems();

            if (voiceChainFinished) return;
            if (!RunFullyHeard(finished)) return;
            voiceChainFinished = true;
            OnVoiceChainFinished?.Invoke();
        }

        /// <summary>
        /// True once every memo of the run that <paramref name="last"/> closes has played to its end.
        /// Both of Marianne's memos sit on screen together, so a visitor can start with the second
        /// one — and the title sequence must not carry them off before they have heard the first.
        /// Also false for a memo that no longer belongs to the open conversation, which is what
        /// keeps an abandoned memo from ever reaching the event.
        /// </summary>
        private bool RunFullyHeard(ContactMessage last)
        {
            var messages = currentContact?.messages;
            if (messages == null) return false;

            var index = messages.IndexOf(last);
            if (index < 0) return false;

            var first = index;
            while (first > 0)
            {
                var previous = messages[first - 1];
                if (!previous.IsVoice || previous.sender != last.sender) break;
                first--;
            }

            for (var i = first; i <= index; i++)
                if (!playedInRun.Contains(messages[i])) return false;

            return true;
        }

        private ContactMessage NextMessage(ContactMessage message)
        {
            var messages = currentContact?.messages;
            if (messages == null) return null;
            var index = messages.IndexOf(message);
            if (index < 0 || index + 1 >= messages.Count) return null;
            return messages[index + 1];
        }

        /// <summary>
        /// Fills the waveform up to the playhead and counts the elapsed time up. Runs every frame
        /// while a memo plays; <see cref="ApplyVoiceProgress"/> skips the bars when nothing moved.
        /// </summary>
        private void UpdateVoiceProgress()
        {
            if (playingRefs == null) return;

            ApplyVoiceProgress(playingRefs, VoiceProgressOf(playingRefs.CurrentMessage));

            if (playingRefs.VoiceTime != null)
                playingRefs.VoiceTime.text = FormatDuration(ElapsedAsSpoken(playingRefs.CurrentMessage));
        }

        /// <summary>
        /// Lifts the bars up to the playhead out of the unplayed grey. Called per frame while a memo
        /// runs, and again on every bind — a paused memo gets no frame updates, and PopulateVoiceBars
        /// hands back freshly built, unfilled bars, so without the bind call the progress of a paused
        /// memo would drop off the screen.
        /// </summary>
        private static void ApplyVoiceProgress(MessageBubbleRefs refs, float progress)
        {
            var bars = refs.VoiceBars;
            if (bars == null) return;

            var barCount = bars.childCount;
            var filled = Mathf.RoundToInt(Mathf.Clamp01(progress) * barCount);
            if (filled == refs.FilledBarCount) return;

            for (var i = 0; i < barCount; i++)
            {
                var bar = bars.ElementAt(i);
                if (i < filled) bar.AddToClassList(VoiceBarPlayedClass);
                else bar.RemoveFromClassList(VoiceBarPlayedClass);
            }

            refs.FilledBarCount = filled;
        }

        /// <summary>
        /// How far the given memo has played, 0 unless it is the loaded one. Paused counts: the
        /// counter simply stops advancing, so the fill stays where the visitor left it.
        /// </summary>
        private float VoiceProgressOf(ContactMessage message)
        {
            if (message != playingMessage || voiceAudioSource == null) return 0f;

            var take = voiceAudioSource.clip;
            if (take == null || take.length <= 0f) return 0f;

            return Mathf.Clamp01(voiceAudioSource.time / take.length);
        }

        /// <summary>
        /// Elapsed time measured against the memo as spoken. The faster takes are shorter, so reading
        /// AudioSource.time straight would let the caption stop well short of the length it shows
        /// while the memo sits idle. Runs faster at 1.5x and 2x, which is the point.
        /// </summary>
        private float ElapsedAsSpoken(ContactMessage message)
        {
            return VoiceProgressOf(message) * DurationOf(ResolveEntry(message));
        }

        /// <summary>
        /// Puts a freshly bound voice row into the right state: pause icon, filled waveform and the
        /// speed pill in place of the avatar while it is the memo the AudioSource holds; play icon,
        /// grey waveform, total length and the avatar otherwise.
        /// </summary>
        private void ApplyVoiceState(MessageBubbleRefs refs, ContactMessage message)
        {
            var isCurrent = message == playingMessage;
            var isRunning = isCurrent && !isPaused;

            if (refs.VoicePlayButton != null)
            {
                if (isRunning) refs.VoicePlayButton.AddToClassList(VoicePlayingClass);
                else refs.VoicePlayButton.RemoveFromClassList(VoicePlayingClass);
            }

            // The avatar gives way to the speed pill for the memo in play. Kept up while paused as
            // well: the rate belongs to the memo the visitor is working through, and taking the pill
            // away on pause would drop the setting out of reach mid-listen.
            if (refs.VoiceAvatar != null)
            {
                refs.VoiceAvatar.style.display = isCurrent ? DisplayStyle.None : DisplayStyle.Flex;
                ApplyAvatar(refs.VoiceAvatar, message.sender == "me" ? selfAvatar : AvatarFor(currentContact));
            }

            if (refs.VoiceSpeedButton != null)
            {
                refs.VoiceSpeedButton.style.display = isCurrent ? DisplayStyle.Flex : DisplayStyle.None;
                refs.VoiceSpeedButton.text = FormatSpeed(VoiceSpeeds[isCurrent ? loadedTakeIndex : voiceSpeedIndex]);
            }

            if (isCurrent) playingRefs = refs;
            else if (playingRefs == refs) playingRefs = null;

            // Paint the fill here rather than leaving it to the frame tick. A paused memo gets no
            // tick at all, and PopulateVoiceBars has just handed back unfilled bars — so this is
            // what keeps a paused memo's progress on screen instead of resetting it to grey.
            ApplyVoiceProgress(refs, VoiceProgressOf(message));

            if (refs.VoiceTime == null) return;

            var seconds = isCurrent ? ElapsedAsSpoken(message) : DurationOf(ResolveEntry(message));
            refs.VoiceTime.text = FormatDuration(seconds);
        }

        private static string FormatDuration(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:D2}";
        }

        private static void PopulateVoiceBars(MessageBubbleRefs refs, string seedKey)
        {
            var bars = refs.VoiceBars;
            if (bars == null) return;
            // Re-generate on every bind: ListView recycles rows, so a recycled
            // voice-memo row may carry bars from a different message.
            bars.Clear();
            // The fresh bars carry no fill, so whatever was painted before no longer holds.
            refs.FilledBarCount = -1;
            var rng = new System.Random((seedKey ?? string.Empty).GetHashCode());
            const int count = 36;
            for (int i = 0; i < count; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("voice-memo__bar");
                bar.style.height = 4f + rng.Next(0, 18);
                bars.Add(bar);
            }
        }

        #endregion

        #region Untertitel

        /// <summary>
        /// Setzt die Zeile, die zum Stand der laufenden Sprachnachricht gehört. Läuft in jedem
        /// Frame mit, in dem gespielt wird; angefasst wird die Leiste aber nur beim Cue-Wechsel.
        ///
        /// Pausiert bleibt die Zeile stehen, weil <see cref="UpdateVoicePlayback"/> dann gar nicht
        /// erst hierher kommt — genau wie die Wellenform, die ebenso stehen bleibt.
        /// </summary>
        private void UpdateSubtitle()
        {
            if (subtitleBar == null) return;

            var message = playingMessage;
            var cues = message?.subtitles;
            if (cues == null || cues.Count == 0)
            {
                HideSubtitle();
                return;
            }

            // Gegen die Nachricht wie gesprochen, nicht gegen den geladenen Take: die Zeiten
            // stehen einmal für 1x in der JSON, und bei 1,5x und 2x läuft der Zähler von selbst
            // schneller. Ohne das müsste jede Zeile pro Geschwindigkeit noch einmal getimt werden.
            ShowCue(message, cues, CueIndexAt(cues, ElapsedAsSpoken(message)));
        }

        /// <summary>
        /// Der letzte Cue, dessen Startzeit erreicht ist, oder -1 vor dem ersten. Setzt voraus,
        /// dass die Cues nach <c>t</c> aufsteigend in der JSON stehen — was für Untertitel ohnehin
        /// die einzig sinnvolle Reihenfolge ist.
        /// </summary>
        private static int CueIndexAt(List<SubtitleCue> cues, float seconds)
        {
            var index = -1;
            for (var i = 0; i < cues.Count; i++)
            {
                if (cues[i] == null) continue;
                if (cues[i].t > seconds) break;
                index = i;
            }
            return index;
        }

        /// <summary>
        /// Ein Cue steht, bis der nächste anfängt; der letzte steht bis zum Ende der Nachricht.
        /// Eine längere Pause mittendrin bekommt deshalb einen Cue mit leerem Text — der räumt die
        /// Leiste, statt die vorige Zeile über der Stille stehen zu lassen.
        /// </summary>
        private void ShowCue(ContactMessage message, List<SubtitleCue> cues, int index)
        {
            if (message == shownCueMessage && index == shownCueIndex) return;

            shownCueMessage = message;
            shownCueIndex = index;

            var line = index >= 0 ? cues[index]?.LocalizedText?.Trim() : null;
            if (string.IsNullOrEmpty(line))
            {
                subtitleBar.style.display = DisplayStyle.None;
                return;
            }

            if (subtitleTextEl != null) subtitleTextEl.text = line;
            subtitleBar.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Räumt die Leiste und vergisst den Cue, sodass dieselbe Zeile danach wieder gesetzt
        /// werden kann. Gehört zu jedem Weg, auf dem eine Nachricht endet: abgebrochen
        /// (<see cref="StopVoice"/>) wie ausgehört (<see cref="HandleVoiceFinished"/>).
        /// </summary>
        private void HideSubtitle()
        {
            shownCueMessage = null;
            shownCueIndex = -1;
            if (subtitleBar != null) subtitleBar.style.display = DisplayStyle.None;
        }

        #endregion

        private static int CountUnread(Contact c)
        {
            if (c.messages == null) return 0;
            int n = 0;
            for (int i = 0; i < c.messages.Count; i++)
            {
                var m = c.messages[i];
                if (m.sender != "me" && m.status == "unread") n++;
            }
            return n;
        }

        private static (string text, string time) LastMessage(Contact c)
        {
            if (c.messages == null || c.messages.Count == 0) return (string.Empty, string.Empty);
            var last = c.messages[c.messages.Count - 1];
            var timeStr = string.Empty;
            if (DateTime.TryParse(last.timestamp, out var dt))
                timeStr = FormatDayHeader(dt);
            var preview = last.LocalizedText?.Replace("\n", " ").Trim() ?? string.Empty;
            return (preview, timeStr);
        }

        private void ShowChatPage(Contact c)
        {
            // Same reasoning as CloseChat: a memo from the previous conversation must not keep the
            // state machine pointed at a message this contact's list does not contain.
            if (currentContact != c) StopVoice();
            currentContact = c;
            MarkIncomingAsRead(c);
            if (chatsPage != null) chatsPage.style.display = DisplayStyle.None;
            if (chatPage != null) chatPage.style.display = DisplayStyle.Flex;
            if (navBar != null) navBar.style.display = DisplayStyle.None;
            if (chatViewName != null) chatViewName.text = c.LocalizedName;
            ApplyAvatar(chatViewAvatar, AvatarFor(c));
            BindMessagesList();
        }

        private void MarkIncomingAsRead(Contact c)
        {
            if (c.messages == null) return;
            bool changed = false;
            for (int i = 0; i < c.messages.Count; i++)
            {
                var m = c.messages[i];
                if (m.sender != "me" && m.status == "unread")
                {
                    m.status = "read";
                    changed = true;
                }
            }
            // Refresh the chat-list row so the unread badge disappears
            // immediately when the user backs out of the conversation.
            if (changed) chatListView?.RefreshItems();
        }

        private void CloseChat()
        {
            // Backing out abandons the memo. Leaving it running would strand playingMessage on a
            // conversation that is no longer open, and NextMessage would then find nothing and
            // report the run as finished — sending the visitor to Scene 2 mid-story.
            StopVoice();
            currentContact = null;
            if (chatsPage != null) chatsPage.style.display = DisplayStyle.Flex;
            if (chatPage != null) chatPage.style.display = DisplayStyle.None;
            if (navBar != null) navBar.style.display = DisplayStyle.Flex;
        }

        private void BindMessagesList()
        {
            if (messagesListView == null) return;
            if (messageBubbleTemplate == null)
            {
                Debug.LogWarning("[Smartphone] 'Message Bubble Template' field is not assigned. Drag Assets/UI/Smartphone/MessageBubble.uxml onto it in the Inspector.", this);
                return;
            }
            if (currentContact == null) return;

            // Assigning makeItem below makes ListView rebuild its pool, so no bind will ever come
            // back with the old refs object and clear this itself.
            playingRefs = null;

            messagesListView.makeItem = () =>
            {
                var row = messageBubbleTemplate.Instantiate();
                var refs = new MessageBubbleRefs
                {
                    Row = row.Q<VisualElement>("messageRow"),
                    Text = row.Q<Label>("messageText"),
                    Time = row.Q<Label>("messageTime"),
                    DateSeparator = row.Q<Label>("dateSeparator"),
                    Status = row.Q<VisualElement>("messageStatus"),
                    VoiceMemo = row.Q<VisualElement>("voiceMemo"),
                    VoiceBars = row.Q<VisualElement>("voiceBars"),
                    VoicePlayButton = row.Q<Button>("voicePlayButton"),
                    VoiceTime = row.Q<Label>("voiceTime"),
                    VoiceAvatar = row.Q<VisualElement>("voiceAvatar"),
                    VoiceSpeedButton = row.Q<Button>("voiceSpeedButton"),
                };
                row.userData = refs;
                if (refs.VoicePlayButton != null)
                {
                    refs.VoicePlayButton.clicked += () => ToggleVoicePlayback(refs);
                }
                if (refs.VoiceSpeedButton != null)
                {
                    refs.VoiceSpeedButton.clicked += () => CycleVoiceSpeed(refs);
                }
                return row;
            };
            messagesListView.bindItem = (element, index) =>
            {
                var refs = element.userData as MessageBubbleRefs;
                if (refs == null || currentContact?.messages == null) return;
                var messages = currentContact.messages;
                var msg = messages[index];
                refs.CurrentMessage = msg;

                var isVoice = msg.IsVoice;
                refs.Text.style.display = isVoice ? DisplayStyle.None : DisplayStyle.Flex;
                refs.Text.text = isVoice ? string.Empty : (msg.LocalizedText?.Trim() ?? string.Empty);
                if (refs.VoiceMemo != null)
                {
                    refs.VoiceMemo.style.display = isVoice ? DisplayStyle.Flex : DisplayStyle.None;
                    if (isVoice)
                    {
                        PopulateVoiceBars(refs, msg.voiceId);
                        ApplyVoiceState(refs, msg);
                    }
                    else if (playingRefs == refs)
                    {
                        playingRefs = null;
                    }
                }

                var hasTimestamp = DateTime.TryParse(msg.timestamp, out var dt);
                refs.Time.text = hasTimestamp ? dt.ToString("HH:mm") : string.Empty;

                // Day separator: show when this message starts a new calendar day.
                bool showSeparator = false;
                if (hasTimestamp && refs.DateSeparator != null)
                {
                    if (index == 0)
                    {
                        showSeparator = true;
                    }
                    else if (DateTime.TryParse(messages[index - 1].timestamp, out var prevDt))
                    {
                        showSeparator = prevDt.Date != dt.Date;
                    }
                    refs.DateSeparator.text = showSeparator ? FormatDayHeader(dt) : string.Empty;
                    refs.DateSeparator.style.display = showSeparator ? DisplayStyle.Flex : DisplayStyle.None;
                }

                // Toggle sender direction. ListView recycles rows so always
                // re-evaluate; never assume the previous binding's class state.
                var isSent = msg.sender == "me";
                if (isSent) refs.Row.AddToClassList("message-row--sent");
                else refs.Row.RemoveFromClassList("message-row--sent");

                // Read-receipt icon. Only sent messages get one; received
                // messages clear all modifiers so the recycler doesn't carry
                // stale state from a previous binding.
                if (refs.Status != null)
                {
                    foreach (var m in StatusModifiers) refs.Status.RemoveFromClassList(m);
                    if (isSent && !string.IsNullOrEmpty(msg.status))
                    {
                        refs.Status.AddToClassList("message-row__status--" + msg.status);
                    }
                }
            };
            messagesListView.itemsSource = currentContact.messages;
            messagesListView.Rebuild();
            messagesListView.RefreshItems();

            // Scroll to the most recent message. Deferred via schedule.Execute
            // so layout settles first — ScrollToItem is a no-op on a 0x0 viewport.
            var lastIndex = currentContact.messages.Count - 1;
            if (lastIndex >= 0)
            {
                messagesListView.schedule.Execute(() => messagesListView.ScrollToItem(lastIndex));
            }
        }

        /// <summary>
        /// One voice memo: the recording as it was spoken, plus the faster takes built from it at
        /// load time. Adding a memo means dropping its clip in here and nothing else — the takes for
        /// the speed pill are derived, never authored.
        /// </summary>
        [Serializable]
        private class ContactAvatar
        {
            public string avatarId;
            public Texture2D picture;
        }

        [Serializable]
        private class VoiceMemoEntry
        {
            public string voiceId;
            public AudioClip clip;

            /// <summary>
            /// One entry per rate in <see cref="VoiceSpeeds"/>, index 0 being the original. Filled in
            /// by <see cref="BuildStretchedTakes"/>; null until that has finished, or for good if the
            /// clip could not be read.
            /// </summary>
            [NonSerialized] public AudioClip[] Takes;
        }

        private class ChatCardRefs
        {
            public Label Name;
            public Label Preview;
            public Label Time;
            public VisualElement Badge;
            public Label BadgeLabel;
            public VisualElement StatusIcon;
            public VisualElement Avatar;
            public Contact CurrentContact;
        }

        private static readonly string[] CardStatusModifiers =
        {
            "chat-card__status-icon--sent",
            "chat-card__status-icon--delivered",
            "chat-card__status-icon--read",
        };

        private class MessageBubbleRefs
        {
            public VisualElement Row;
            public Label Text;
            public Label Time;
            public Label DateSeparator;
            public VisualElement Status;
            public VisualElement VoiceMemo;
            public VisualElement VoiceBars;
            public Button VoicePlayButton;
            public Label VoiceTime;
            public VisualElement VoiceAvatar;
            public Button VoiceSpeedButton;
            public ContactMessage CurrentMessage;
            /// <summary>How many bars this row currently paints as played. -1 means "unknown".</summary>
            public int FilledBarCount = -1;
        }

        private static readonly string[] StatusModifiers =
        {
            "message-row__status--sent",
            "message-row__status--delivered",
            "message-row__status--read",
        };

        private static readonly string[] GermanMonths =
        {
            "Januar", "Februar", "März", "April", "Mai", "Juni",
            "Juli", "August", "September", "Oktober", "November", "Dezember",
        };

        private static readonly string[] EnglishMonths =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };

        private static bool IsEnglish => Node.CurrentLanguage == Language.En;

        private static string FormatDayHeader(DateTime when)
        {
            var msg = when.Date;
            var today = DateTime.Now.Date;

            if (IsEnglish)
            {
                if (msg == today) return "Today";
                if (msg == today.AddDays(-1)) return "Yesterday";
                // Day and month swap around in English, and the day loses its ordinal dot.
                return $"{EnglishMonths[msg.Month - 1]} {msg.Day}, {msg.Year}";
            }

            if (msg == today) return "Heute";
            if (msg == today.AddDays(-1)) return "Gestern";
            return $"{msg.Day:D2}. {GermanMonths[msg.Month - 1]} {msg.Year}";
        }

        [Serializable]
        private class ContactsRoot
        {
            public List<Contact> contacts;
        }

        [Serializable]
        private class Contact
        {
            public string contact;
            public string contactEn;
            public string avatarId; // key into the Contact Avatars list on Smartphone
            public List<ContactMessage> messages;

            /// <summary>
            /// Falls back to the German name while no translation is in the JSON, same as the
            /// dialog lines do — a nameless chat card must never reach the kiosk.
            /// </summary>
            public string LocalizedName =>
                Node.CurrentLanguage == Language.En && !string.IsNullOrWhiteSpace(contactEn)
                    ? contactEn
                    : contact;
        }

        /// <summary>
        /// Eine Untertitelzeile einer Sprachnachricht. <c>t</c> ist ihr Anfang in Sekunden,
        /// gemessen an der Aufnahme wie gesprochen (1x) — die schnelleren Takes rechnet
        /// <see cref="ElapsedAsSpoken"/> darauf zurück. Die Zeile steht, bis der nächste Cue
        /// anfängt; ein Cue mit leerem Text räumt die Leiste wieder.
        /// </summary>
        [Serializable]
        private class SubtitleCue
        {
            public float t;
            public string text;
            public string textEn;

            /// <summary>
            /// Fällt auf Deutsch zurück, solange keine Übersetzung in der JSON steht — dieselbe
            /// Regel wie bei den Chatnachrichten. Leerer Text ist dabei kein Fehlen, sondern die
            /// gewollte Pause, und bleibt deshalb in beiden Sprachen leer.
            /// </summary>
            public string LocalizedText =>
                Node.CurrentLanguage == Language.En && !string.IsNullOrWhiteSpace(textEn)
                    ? textEn
                    : text;
        }

        [Serializable]
        private class ContactMessage
        {
            public string text;
            public string textEn;
            public string timestamp;
            public string sender; // "me" → right-aligned blue bubble; anything else → received
            public string status; // sent: "sent" | "delivered" | "read". received: "read" | "unread".
            public string type;   // "" (text) | "voice"
            public string voiceId; // "voice" only: key into the Voice Memo Clips list on Smartphone
            public List<SubtitleCue> subtitles; // "voice" only: Untertitel, nach t aufsteigend

            public bool IsVoice => type == "voice";

            public string LocalizedText =>
                Node.CurrentLanguage == Language.En && !string.IsNullOrWhiteSpace(textEn)
                    ? textEn
                    : text;
        }
    }
}
