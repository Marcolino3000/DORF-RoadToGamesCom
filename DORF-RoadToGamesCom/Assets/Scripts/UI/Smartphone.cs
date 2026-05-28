using System;
using System.Collections.Generic;
using DefaultNamespace;
using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class Smartphone : MonoBehaviour
    {
        [SerializeField] private Raycaster raycaster;

        [Header("Status Bar")]
        [SerializeField] private string time = "9:41";
        [SerializeField] private string cellularLabel = "3G";

        [Header("Chats")]
        [SerializeField] private TextAsset contactsJson;
        [SerializeField] private VisualTreeAsset chatCardTemplate;
        [SerializeField] private VisualTreeAsset messageBubbleTemplate;

        [SerializeField] private TitleSequenceTrigger titleSequenceTrigger;
        [SerializeField] private bool sprachiWasTriggered;

        private UIDocument uiDocument;
        private VisualElement root;
        private Label timeLabelEl;
        private Label cellularLabelEl;
        private ListView chatListView;

        // Conversation page elements.
        private VisualElement chatsPage;
        private VisualElement chatPage;
        private VisualElement navBar;
        private Button backButton;
        private Label chatViewName;
        private ListView messagesListView;

        private readonly List<Contact> contacts = new();
        private Contact currentContact;
        private bool isOpen;
        // Tracks which voice memo (keyed by timestamp) is currently "playing"
        // so the play/pause icon survives ListView recycling and rebuilds.
        private string playingVoiceKey;

        private void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            LoadContacts();
            BindRoot(initial: true);
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

            chatsPage = root.Q<VisualElement>("chatsPage");
            chatPage = root.Q<VisualElement>("chatPage");
            navBar = root.Q<VisualElement>("navBar");
            backButton = root.Q<Button>("backButton");
            chatViewName = root.Q<Label>("chatViewName");
            messagesListView = root.Q<ListView>("messagesListView");

            if (backButton != null)
            {
                backButton.clicked -= CloseChat;
                backButton.clicked += CloseChat;
            }

            ApplyStatusBar();
            BindChatList();

            // After a hot reload, restore the conversation if one was open.
            if (currentContact != null)
            {
                ShowChatPage(currentContact);
            }

            SetVisible(initial ? false : isOpen);
        }

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
            if (raycaster != null) raycaster.isDialogRunning = true;
            // ListView in a hidden panel has a 0x0 viewport and won't
            // create rows. Refresh once visible so cards appear.
            chatListView?.RefreshItems();
        }

        public void Close()
        {
            SetVisible(false);
            if (raycaster != null) raycaster.isDialogRunning = false;
        }

        private void ApplyStatusBar()
        {
            if (timeLabelEl != null) timeLabelEl.text = time;
            if (cellularLabelEl != null) cellularLabelEl.text = cellularLabel;
        }

        private void SetVisible(bool visible)
        {
            isOpen = visible;
            if (root == null) return;
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
            Debug.Log($"[Smartphone] Loaded {contacts.Count} contacts.", this);
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

                refs.Name.text = c.contact;

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

        private const string VoicePlayingClass = "voice-memo__play--playing";

        private void ToggleVoicePlayback(MessageBubbleRefs refs)
        {
            if (sprachiWasTriggered)
                return;
            
            if (refs?.CurrentVoiceKey == null) return;
            // Single-track playback: starting one memo stops any other.
            playingVoiceKey = playingVoiceKey == refs.CurrentVoiceKey ? null : refs.CurrentVoiceKey;
            // Re-bind every visible row so the previously-playing memo (if any)
            // reverts its icon. Cheap — only ~10 rows are realised at a time.
            messagesListView?.RefreshItems();
            
            titleSequenceTrigger.StartSprachiDialog();
            sprachiWasTriggered = true;
        }

        private void ApplyVoicePlayingClass(MessageBubbleRefs refs)
        {
            if (refs?.VoicePlayButton == null) return;
            var isPlaying = refs.CurrentVoiceKey != null && refs.CurrentVoiceKey == playingVoiceKey;
            if (isPlaying) refs.VoicePlayButton.AddToClassList(VoicePlayingClass);
            else refs.VoicePlayButton.RemoveFromClassList(VoicePlayingClass);
        }

        private static void PopulateVoiceBars(VisualElement bars, string seedKey)
        {
            if (bars == null) return;
            // Re-generate on every bind: ListView recycles rows, so a recycled
            // voice-memo row may carry bars from a different message.
            bars.Clear();
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
            var preview = last.text?.Replace("\n", " ").Trim() ?? string.Empty;
            return (preview, timeStr);
        }

        private void ShowChatPage(Contact c)
        {
            currentContact = c;
            MarkIncomingAsRead(c);
            if (chatsPage != null) chatsPage.style.display = DisplayStyle.None;
            if (chatPage != null) chatPage.style.display = DisplayStyle.Flex;
            if (navBar != null) navBar.style.display = DisplayStyle.None;
            if (chatViewName != null) chatViewName.text = c.contact;
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
                };
                row.userData = refs;
                if (refs.VoicePlayButton != null)
                {
                    refs.VoicePlayButton.clicked += () => ToggleVoicePlayback(refs);
                }
                return row;
            };
            messagesListView.bindItem = (element, index) =>
            {
                var refs = element.userData as MessageBubbleRefs;
                if (refs == null || currentContact?.messages == null) return;
                var messages = currentContact.messages;
                var msg = messages[index];
                var isVoice = msg.type == "voice";
                refs.Text.style.display = isVoice ? DisplayStyle.None : DisplayStyle.Flex;
                refs.Text.text = isVoice ? string.Empty : (msg.text?.Trim() ?? string.Empty);
                refs.CurrentVoiceKey = isVoice ? msg.timestamp : null;
                if (refs.VoiceMemo != null)
                {
                    refs.VoiceMemo.style.display = isVoice ? DisplayStyle.Flex : DisplayStyle.None;
                    if (isVoice)
                    {
                        PopulateVoiceBars(refs.VoiceBars, msg.timestamp);
                        ApplyVoicePlayingClass(refs);
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

        private class ChatCardRefs
        {
            public Label Name;
            public Label Preview;
            public Label Time;
            public VisualElement Badge;
            public Label BadgeLabel;
            public VisualElement StatusIcon;
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
            public string CurrentVoiceKey;
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

        private static string FormatDayHeader(DateTime when)
        {
            var msg = when.Date;
            var today = DateTime.Now.Date;
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
            public List<ContactMessage> messages;
        }

        [Serializable]
        private class ContactMessage
        {
            public string text;
            public string timestamp;
            public string sender; // "me" → right-aligned blue bubble; anything else → received
            public string status; // sent: "sent" | "delivered" | "read". received: "read" | "unread".
            public string type;   // "" (text) | "voice"
        }
    }
}
