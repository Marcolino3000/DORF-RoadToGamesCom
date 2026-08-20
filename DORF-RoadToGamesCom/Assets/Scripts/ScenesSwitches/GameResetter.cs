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
