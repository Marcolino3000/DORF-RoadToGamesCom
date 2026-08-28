using System;
using System.Collections.Generic;
using System.Text;
using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Makes the Sauerteig jar in the corner of the UI behave like an interactable under the mouse.
    /// The jar is a UGUI Image and the Raycaster only ever sees physics colliders, so on its own it
    /// shows no cursor symbol for the jar and does not know that a click there belongs to the UI.
    /// Both are handled from this side:
    ///
    /// * the cursor comes from the interactable's own InteractionType - the same value the Raycaster
    ///   hands the CursorSetter for a world interactable, so the jar gets the inspect symbol without
    ///   a second copy of that decision living here;
    /// * <see cref="Raycaster.IsMenuOpen"/> is raised while the pointer is on the jar. That is the
    ///   flag the Raycaster honours unconditionally, in Update and in HandleMouseClick alike, so the
    ///   ground behind the jar stops answering the click and Marlene stays where she is. Same lever
    ///   the Smartphone pulls while it is open.
    ///
    /// While a dialog runs the jar answers nothing at all - see <see cref="DialogBlocksJar"/>. The
    /// Raycaster's own dialog gates cannot do that job here: they sit in front of the physics
    /// raycast, and this click arrives through the EventSystem instead.
    ///
    /// The click plays the comment itself, by calling Execute on the Reaction for the jar's current
    /// activity level. That deliberately skips InteractionHandler and InteractionViewer: the click
    /// does reach a live Interactable with a subscriber, but the trigger never comes out the other
    /// end, and a comment on the jar needs none of what that chain adds - no counters, no thresholds,
    /// no recorded-state conditions.
    /// </summary>
    public class SauerteigUiPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, ISceneSetupCallbackReceiver
    {
        [Header("References")]
        [Tooltip("The jar's own state asset (SauerteigAwarenessStatus). Only its InteractionType is " +
                 "read, so the cursor follows whatever the asset says instead of a copy kept here.")]
        [SerializeField] private InteractableState interactableState;
        [Tooltip("These two sit on the Global prefab, which the Bootstrapper spawns from Resources at " +
                 "runtime - so they can only be assigned here if this component sits on that prefab " +
                 "too. Left empty they are looked up once, at Awake.")]
        [SerializeField] private Raycaster raycaster;
        [SerializeField] private CursorSetter cursorSetter;

        [Header("Comment")]
        [Tooltip("Played on click. The level the state asset currently reports picks the entry, so the " +
                 "jar can answer differently as its activity rises.")]
        [SerializeField] private LevelReaction[] reactionsPerLevel;
        [Tooltip("Played when no entry above matches the current level.")]
        [SerializeField] private Reaction fallbackReaction;

        [Header("Dialog")]
        [Tooltip("While a dialog runs the jar plays no comment and shows no inspect symbol. The click " +
                 "itself still reaches DialogTreeRunner, which is what skips to the next line - so " +
                 "clicking the jar mid-sentence does the same as clicking anywhere else.")]
        [SerializeField] private bool ignoreDuringDialog = true;
        [Tooltip("Narrower version of the setting above: only a conversation with another character " +
                 "counts, Marlene's own comments do not - the same split the Raycaster makes. Without " +
                 "effect while the setting above is on.")]
        [SerializeField] private bool ignoreDuringCharacterDialog = true;

        [Header("Settings")]
        [Tooltip("Used when no state asset is assigned.")]
        [SerializeField] private InteractionType fallbackInteractionType = InteractionType.Inspect;
        [Tooltip("Writes one report per left click: what the EventSystem actually hit, and whether the " +
                 "Sauerteig's Interactable is in a state where the click can reach the InteractionHandler. " +
                 "Turn off once the jar answers clicks.")]
        [SerializeField] private bool logClicks = true;

        [Serializable]
        private struct LevelReaction
        {
            public AwarenessLevel Level;
            public Reaction Reaction;
        }

        // read these in the inspector while playing: they show who currently owns the input block
        [Header("Debug")]
        [SerializeField] private bool pointerIsOver;
        [SerializeField] private bool raisedMenuFlag;
        [SerializeField] private bool dialogBlocked;

        private void Awake()
        {
            // The Bootstrapper instantiates Global before the first scene loads, so by Awake both
            // are there. One lookup covers the case where this component cannot reference them.
            if (raycaster == null)
                raycaster = FindFirstObjectByType<Raycaster>();

            if (cursorSetter == null)
                cursorSetter = FindFirstObjectByType<CursorSetter>();

            if (raycaster == null || cursorSetter == null)
            {
                Debug.LogError($"{nameof(SauerteigUiPointer)} on '{name}' found no " +
                               $"{(raycaster == null ? nameof(Raycaster) : nameof(CursorSetter))}.", this);
                enabled = false;
                return;
            }

            // Without a raycast target on this object the pointer events never arrive and the whole
            // component is silently dead, which is exactly the kind of failure worth a line in the log.
            var graphic = GetComponent<Graphic>();

            if (graphic == null || !graphic.raycastTarget)
                Debug.LogError($"{nameof(SauerteigUiPointer)} on '{name}' needs a Graphic with " +
                               "Raycast Target on, otherwise it never sees the pointer.", this);
        }

        /// <summary>
        /// The Raycaster carries both flags for everybody: it sets them from
        /// DialogTreeRunner.OnDialogRunningStatusChanged and clears them in ResetState, so a visitor
        /// who walks away mid-dialog does not leave the jar mute for the next one.
        /// isCharacterDialogRunning is a subset of isDialogRunning, hence the two separate toggles.
        /// </summary>
        private bool DialogBlocksJar =>
            raycaster != null &&
            ((ignoreDuringDialog && raycaster.isDialogRunning) ||
             (ignoreDuringCharacterDialog && raycaster.isCharacterDialogRunning));

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Read before BlockWorldInput, which is what makes the flag ours: whoever already holds
            // it is a menu or the phone, both of which cover the jar and show the standard cursor.
            // Painting the inspect symbol on top of them is the one thing that gets past MenuToggle.
            var blockedByOther = raycaster.IsMenuOpen && !raisedMenuFlag;

            pointerIsOver = true;

            BlockWorldInput();

            if (blockedByOther)
                return;

            // A symbol on a jar that answers nothing is the one thing a kiosk visitor reads as a
            // broken click, so the cursor stays standard for as long as the dialog does.
            if (DialogBlocksJar)
                return;

            cursorSetter.SetCursor(interactableState != null
                ? interactableState.InteractionType
                : fallbackInteractionType);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Both dialogs that reach here are worth stopping: one Marlene is having with somebody,
            // and the jar's own comment, whose Reaction would otherwise cut itself off and restart.
            if (DialogBlocksJar)
            {
                if (logClicks)
                    Debug.Log($"{nameof(SauerteigUiPointer)}: click ignored, a dialog is running.", this);

                return;
            }

            var level = GetLevel();
            var reaction = GetReaction(level);

            if (reaction == null)
            {
                Debug.LogWarning($"{nameof(SauerteigUiPointer)} on '{name}': nothing to play for " +
                                 $"level {level} - assign a Reaction for it, or a fallback.", this);
                return;
            }

            if (logClicks)
                Debug.Log($"{nameof(SauerteigUiPointer)}: playing '{reaction.name}' for level {level}.", this);

            // Reaction.Execute stops whatever dialog is running before it sets its own tree, so
            // clicking again simply restarts the comment - the same thing self-talk does elsewhere.
            reaction.Execute();
        }

        private AwarenessLevel GetLevel()
        {
            return interactableState is SauerteigState sauerteigState
                ? sauerteigState.CurrentLevel
                : AwarenessLevel.NotSet;
        }

        private Reaction GetReaction(AwarenessLevel level)
        {
            if (reactionsPerLevel != null)
            {
                foreach (var entry in reactionsPerLevel)
                {
                    if (entry.Level == level && entry.Reaction != null)
                        return entry.Reaction;
                }
            }

            return fallbackReaction;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerIsOver = false;

            ReleaseWorldInput();

            // The Raycaster picks the cursor up again from the next Update; this only keeps the
            // inspect symbol from lingering for that one frame.
            cursorSetter.SetCursor(InteractionType.None);
        }

        /// <summary>
        /// IsMenuOpen is one shared bool, so ownership is tracked rather than the previous value
        /// restored: if a menu or the phone already holds it, the jar leaves it alone and does not
        /// lower it on the way out either.
        /// </summary>
        private void BlockWorldInput()
        {
            if (raisedMenuFlag || raycaster.IsMenuOpen)
                return;

            raycaster.IsMenuOpen = true;
            raisedMenuFlag = true;
        }

        private void ReleaseWorldInput()
        {
            if (!raisedMenuFlag)
                return;

            raycaster.IsMenuOpen = false;
            raisedMenuFlag = false;
        }

        // A scene load can replace the EventSystem, so the matching OnPointerExit may never arrive.
        // Releasing here is what keeps a half-finished hover from blocking world input for good.
        public void OnSceneSetup()
        {
            pointerIsOver = false;
            ReleaseWorldInput();
        }

        private void OnDisable()
        {
            pointerIsOver = false;
            ReleaseWorldInput();
        }

        private void Update()
        {
            UpdateCursorForDialogState();

            if (!logClicks || !Input.GetMouseButtonDown(0))
                return;

            LogClickReport();
        }

        /// <summary>
        /// A dialog can start and end while the pointer rests on the jar, and neither raises a pointer
        /// event - so the inspect symbol would stay up through a dialog that swallows the click, or
        /// stay missing after one. The Raycaster cannot correct it either: hovering the jar holds
        /// IsMenuOpen, and its Update returns on that flag before it ever looks at the cursor.
        /// Only the change is written, so this does not fight whoever else owns the cursor meanwhile.
        /// </summary>
        private void UpdateCursorForDialogState()
        {
            var blocked = DialogBlocksJar;

            if (blocked == dialogBlocked)
                return;

            dialogBlocked = blocked;

            // Same read as OnPointerEnter: a menu or the phone on top of the jar owns the cursor.
            if (!pointerIsOver || (raycaster.IsMenuOpen && !raisedMenuFlag))
                return;

            cursorSetter.SetCursor(blocked
                ? InteractionType.None
                : interactableState != null
                    ? interactableState.InteractionType
                    : fallbackInteractionType);
        }

        /// <summary>
        /// Runs the EventSystem's own raycast at the pointer rather than waiting to be clicked, so the
        /// report arrives even when something else is on top and swallows the click. The first entry
        /// in the hit list is the one that gets it.
        /// </summary>
        private void LogClickReport()
        {
            var report = new StringBuilder();
            report.AppendLine($"{nameof(SauerteigUiPointer)} on '{name}': click at {Input.mousePosition}");

            if (EventSystem.current == null)
            {
                report.AppendLine("  EventSystem.current is null - no UI click can arrive at all.");
                Debug.LogWarning(report.ToString(), this);
                return;
            }

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = Input.mousePosition }, hits);

            report.AppendLine($"  UI raycast hits ({hits.Count}), first one takes the click:");

            foreach (var hit in hits)
                report.AppendLine($"    {hit.gameObject.name} (module {hit.module.GetType().Name}, " +
                                  $"sortingOrder {hit.sortingOrder}, depth {hit.depth})");

            var onTop = hits.Count > 0 && hits[0].gameObject == gameObject;
            report.AppendLine($"  this object is the top hit: {onTop}");
            report.AppendLine($"  IsMenuOpen: {raycaster.IsMenuOpen} (raised here: {raisedMenuFlag})");

            if (interactableState == null)
            {
                report.AppendLine("  no state asset assigned on this component.");
                Debug.Log(report.ToString(), this);
                return;
            }

            report.AppendLine($"  state '{interactableState.name}', InteractionType {interactableState.InteractionType}");

            // The condition on every InteractionViewer record for the jar keys on this level, so a
            // value none of them records is the whole explanation for a click that does nothing.
            if (interactableState is SauerteigState sauerteigState)
                report.AppendLine($"  SauerteigState.CurrentLevel: {sauerteigState.CurrentLevel}");

            // Set by Interactable.OnEnable. Null means that never ran, and UiInteractionTrigger has
            // nothing to post the click to.
            var interactable = interactableState.Interactable;

            report.AppendLine(interactable == null
                ? "  state.Interactable is NULL - Interactable.OnEnable never ran."
                : $"  Interactable '{interactable.name}', subscribers on OnInteractionStarted: " +
                  $"{interactable.OnInteractionStarted?.GetInvocationList().Length ?? 0} " +
                  "(0 means InteractionHandler.FindClients never reached it)");

            // Debug.Log(report.ToString(), this);
        }
    }
}
