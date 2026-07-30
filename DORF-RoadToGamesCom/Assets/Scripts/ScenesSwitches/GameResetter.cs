using System;
using System.Collections;
using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ScenesSwitches
{
    /// <summary>
    /// Handles the reset that InputDispatcher.OnGameReset fires after the inactivity timeout:
    /// resets the state of all interactables, shows a video over the running scene and restarts
    /// the game at the first scene as soon as a visitor presses any key or clicks the mouse.
    /// </summary>
    public class GameResetter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string firstSceneName = "Scene 1";
        [SerializeField] private bool loopVideoUntilInput = true;
        [SerializeField] private float videoPrepareTimeout = 5f;
        [SerializeField] private bool debugLogs;

        [Header("References")]
        [SerializeField] private SceneSetup sceneSetup;
        [SerializeField] private GameObject videoOverlay;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoImage;

        private IDisposable _anyButtonListener;
        private bool _resetRunning;
        private bool _restarting;

        /// <summary>
        /// Entry point for InputDispatcher.OnGameReset.
        /// </summary>
        public void ResetGame()
        {
            // The dispatcher keeps firing its timeout as long as nobody touches the machine.
            if (_resetRunning)
                return;

            _resetRunning = true;

            if (debugLogs)
                Debug.Log("GameResetter: game reset triggered");

            ResetInteractables();

            // The player character must not walk around behind the video.
            PlayerController.EnableMovement(false);

            StartCoroutine(ShowVideoAndWaitForInput());
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

        private IEnumerator ShowVideoAndWaitForInput()
        {
            // Armed before the video is up so the very first press always counts.
            ArmAnyButtonListener();

            if (!HasVideoToShow())
                yield break;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loopVideoUntilInput;

            // APIOnly renders into the player's own texture, so no RenderTexture asset is needed.
            videoPlayer.renderMode = VideoRenderMode.APIOnly;

            // While looping, loopPointReached fires on every pass, so it is only useful as an
            // "video is over, nobody came" trigger for one-shot videos.
            if (!loopVideoUntilInput)
                videoPlayer.loopPointReached += HandleVideoFinished;

            videoImage.enabled = false;
            videoOverlay.SetActive(true);

            videoPlayer.Prepare();

            var deadline = Time.realtimeSinceStartup + videoPrepareTimeout;
            while (!videoPlayer.isPrepared && Time.realtimeSinceStartup < deadline)
            {
                if (_restarting)
                    yield break;

                yield return null;
            }

            if (!videoPlayer.isPrepared)
                Debug.LogWarning($"GameResetter: video was not ready within {videoPrepareTimeout} seconds, playing anyway.");

            videoPlayer.Play();

            // In APIOnly mode the texture only exists once the player has content, which can be a
            // frame later than isPrepared. Keep the image off until then so no white quad flashes up.
            deadline = Time.realtimeSinceStartup + videoPrepareTimeout;
            while (videoPlayer.texture == null && Time.realtimeSinceStartup < deadline)
            {
                if (_restarting)
                    yield break;

                yield return null;
            }

            videoImage.texture = videoPlayer.texture;
            videoImage.enabled = true;

            if (debugLogs)
                Debug.Log("GameResetter: reset video is playing");
        }

        private bool HasVideoToShow()
        {
            if (videoPlayer == null || videoOverlay == null || videoImage == null)
            {
                Debug.LogError("GameResetter: video overlay is not set up, waiting for input without a video.");
                return false;
            }

            var hasContent = videoPlayer.source == VideoSource.VideoClip
                ? videoPlayer.clip != null
                : !string.IsNullOrEmpty(videoPlayer.url);

            if (!hasContent)
            {
                Debug.LogWarning("GameResetter: no video assigned to the VideoPlayer, waiting for input without a video.");
                return false;
            }

            return true;
        }

        private void ArmAnyButtonListener()
        {
            _anyButtonListener?.Dispose();

            // Covers every key, every mouse button and every gamepad button on any connected device.
            _anyButtonListener = InputSystem.onAnyButtonPress.CallOnce(control =>
            {
                if (debugLogs)
                    Debug.Log($"GameResetter: restarting after press on {control.path}");

                RestartAtFirstScene();
            });
        }

        private void HandleVideoFinished(VideoPlayer player)
        {
            if (debugLogs)
                Debug.Log("GameResetter: reset video finished without input, restarting anyway");

            RestartAtFirstScene();
        }

        private void RestartAtFirstScene()
        {
            if (_restarting)
                return;

            _restarting = true;

            StopListening();

            PlayerController.EnableMovement(true);

            // The video stays up while SceneSwapManager fades to black and is hidden once the first
            // scene is in, so the swap happens behind a black screen.
            SceneManager.sceneLoaded += HandleFirstSceneLoaded;

            SceneSwapManager.ChangeScene(firstSceneName);
        }

        private void HandleFirstSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleFirstSceneLoaded;

            HideVideo();

            _restarting = false;
            _resetRunning = false;
        }

        private void HideVideo()
        {
            if (videoPlayer != null)
                videoPlayer.Stop();

            if (videoImage != null)
            {
                videoImage.enabled = false;
                videoImage.texture = null;
            }

            if (videoOverlay != null)
                videoOverlay.SetActive(false);
        }

        private void StopListening()
        {
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;

            if (videoPlayer != null)
                videoPlayer.loopPointReached -= HandleVideoFinished;
        }

        private void OnDisable()
        {
            StopListening();

            SceneManager.sceneLoaded -= HandleFirstSceneLoaded;
        }
    }
}
