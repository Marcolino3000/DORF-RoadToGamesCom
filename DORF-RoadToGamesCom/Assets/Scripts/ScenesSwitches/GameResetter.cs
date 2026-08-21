using System.Reflection;
using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScenesSwitches
{
    /// <summary>
    /// Handles the reset that InputDispatcher.OnGameReset fires after the inactivity timeout:
    /// resets the state of all interactables and restarts the game at the first scene. StartSplash
    /// listens for that scene itself and comes back up, so the machine ends up on the start image
    /// waiting for the next visitor to pick a language.
    /// </summary>
    public class GameResetter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string firstSceneName = "Scene 1";
        [SerializeField] private bool debugLogs;

        [Header("References")]
        [SerializeField] private SceneSetup sceneSetup;

        [Header("Sauerteig")]
        [Tooltip("Both sit on the Global prefab. Left empty they are looked up once, on the first reset.")]
        [SerializeField] private Sauerteig sauerteig;
        [SerializeField] private SauerteigStatusDisplay sauerteigStatusDisplay;
        [Tooltip("Activity the Sauerteig starts a play-through with. SetActivity clamps to 1 anyway, " +
                 "so anything below that only delays the first step.")]
        [SerializeField] private int lockedActivity = 1;
        [Tooltip("Level the SauerteigAwarenessStatus asset carries before the jar is unlocked. The " +
                 "asset keeps whatever the finished play-through left on it, so it has to be written back.")]
        [SerializeField] private AwarenessLevel lockedAwarenessLevel = AwarenessLevel.Basic;

        private bool _resetRunning;

        /// <summary>
        /// Entry point for InputDispatcher.OnGameReset.
        /// </summary>
        public void ResetGame()
        {
            // The dispatcher keeps firing its timeout as long as nobody touches the machine.
            if (_resetRunning)
                return;

            // Nobody has started a game yet: the start image is the attract screen already, and a
            // reset behind it would make the next press restart the game and skip the start screen
            // in one go. Leave it up and let the dispatcher fire again after the next timeout.
            if (StartSplash.IsShowing)
                return;

            _resetRunning = true;

            if (debugLogs)
                Debug.Log("GameResetter: game reset triggered");

            ResetInteractables();

            ResetSauerteig();

            // The player character must not walk around while the screen fades out.
            PlayerController.EnableMovement(false);

            RestartAtFirstScene();
        }

        private void ResetInteractables()
        {
            if (sceneSetup == null)
            {
                Debug.LogError("GameResetter: no SceneSetup assigned, interactables keep their state.");
                return;
            }

            sceneSetup.SetupScene();
        }

        /// <summary>
        /// ShowSauerteig() activates the jar but has no counterpart, so the object to switch off is
        /// read from the field the display itself uses rather than looked up by name: re-hang the jar
        /// in the prefab and the reset still finds it. A Hide() in com.cod.interactionbuilder would be
        /// the tidier home for this, at the price of a package version.
        /// </summary>
        private static readonly FieldInfo JarField = typeof(SauerteigStatusDisplay)
            .GetField("Sauerteig", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// The Sauerteig and its jar both live on the Global prefab, so they outlast every scene
        /// load: once a visitor unlocked the jar it would stay on screen for the next one, with
        /// IsUnlocked still true and the dough at the height the last play-through left it. That is
        /// play-through state, so it is cleared here and not in OnSceneSetup - that one runs on every
        /// scene load and would lock the jar again in the middle of a game.
        /// </summary>
        private void ResetSauerteig()
        {
            if (sauerteig == null)
                sauerteig = FindFirstObjectByType<Sauerteig>(FindObjectsInactive.Include);

            if (sauerteig == null)
            {
                Debug.LogWarning("GameResetter: no Sauerteig found, it stays unlocked.");
            }
            else
            {
                sauerteig.IsUnlocked = false;
                sauerteig.Activity = lockedActivity;

                // The level sits on a ScriptableObject, which in the Editor is even written back to
                // disk - without this the next run starts at the level the last one reached.
                if (sauerteig.State != null)
                    sauerteig.State.CurrentLevel = lockedAwarenessLevel;
            }

            if (sauerteigStatusDisplay == null)
                sauerteigStatusDisplay = FindFirstObjectByType<SauerteigStatusDisplay>(FindObjectsInactive.Include);

            if (sauerteigStatusDisplay == null)
            {
                Debug.LogWarning("GameResetter: no SauerteigStatusDisplay found, the jar stays visible.");
                return;
            }

            // Shrinks the dough back to its starting height while nobody can see the jar. The
            // coroutine runs on the display object, which stays active, so hiding the jar right
            // after does not cut it short - and SauerteigDoughAnimation follows the shrinking scale
            // back down to its first frame on its own.
            sauerteigStatusDisplay.UpdateStatus(lockedActivity);

            var jar = JarField?.GetValue(sauerteigStatusDisplay) as GameObject;

            if (jar == null)
            {
                Debug.LogWarning("GameResetter: the SauerteigStatusDisplay has no jar assigned, " +
                                 "nothing to hide.", sauerteigStatusDisplay);
                return;
            }

            jar.SetActive(false);

            if (debugLogs)
                Debug.Log("GameResetter: Sauerteig locked again and jar hidden");
        }

        private void RestartAtFirstScene()
        {
            SceneManager.sceneLoaded += HandleFirstSceneLoaded;

            // SceneSwapManager fades to black before it swaps, so nothing of the abandoned
            // play-through is visible while the first scene comes in behind the start image.
            SceneSwapManager.ChangeScene(firstSceneName);
        }

        private void HandleFirstSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleFirstSceneLoaded;

            // StartSplash subscribes to sceneLoaded in Awake, so it has already put itself back up
            // by now and owns movement until it fades. Only when there is no start image at all —
            // no artwork to show — would nobody ever switch movement on again.
            if (!StartSplash.IsShowing)
                PlayerController.EnableMovement(true);

            _resetRunning = false;

            if (debugLogs)
                Debug.Log("GameResetter: restarted at the start screen");
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleFirstSceneLoaded;
        }
    }
}
