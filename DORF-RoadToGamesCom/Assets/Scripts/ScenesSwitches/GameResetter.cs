using System.Reflection;
using Runtime.Scripts.Core;
using Runtime.Scripts.Interactables;
using Runtime.Scripts.PlayerInput;
using SceneManagement;
using Tree;
using UnityEngine;
using UnityEngine.AI;
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

        [Header("Marlene")]
        [Tooltip("Sits on the Global prefab as well. Left empty she is looked up once, on the first reset.")]
        [SerializeField] private PlayerController player;

        [Header("Scripted sequences")]
        [Tooltip("On the Global prefab as well. Left empty it is looked up once, on the first reset.")]
        [SerializeField] private SequenceRunner sequenceRunner;

        [Header("Input state")]
        [Tooltip("Both sit on the Global prefab too. Left empty they are looked up once, on the " +
                 "first reset.")]
        [SerializeField] private Raycaster raycaster;
        [SerializeField] private DialogTreeRunner dialogTreeRunner;

        private bool _resetRunning;
        private bool _startPoseKnown;
        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;

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

            // Ahead of EnableMovement(false): MovementDisabler answers the dialog's running-status
            // change by switching movement back on, so a dialog aborted afterwards would undo it.
            AbortRunningDialog();

            AbortRunningSequence();

            ResetInputState();

            // The player character must not walk around while the screen fades out.
            PlayerController.EnableMovement(false);

            StopPlayer();

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

        /// <summary>
        /// Cancels whatever Marlene was doing when the reset came in. The click-to-move NavMeshAgent
        /// keeps its destination across the scene load and PlayerController's move coroutine keeps
        /// running, so without this she walks off the start position again the moment movement is
        /// switched back on.
        /// </summary>
        private void StopPlayer()
        {
            if (!FindPlayer())
                return;

            // drops the running move coroutine and zeroes the velocity
            player.MoveInDirection(Vector2.zero);

            // MoveByClick keeps its own idea of whether the agent is on its way, and it is the one
            // that tells everybody else when a walk has ended. Resetting the path behind its back
            // leaves it waiting on a movement that can never end.
            var moveByClick = player.GetComponent<MoveByClick>();

            if (moveByClick != null)
            {
                moveByClick.CancelMovement();
                return;
            }

            var agent = player.GetComponent<NavMeshAgent>();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }

        /// <summary>
        /// A scripted sequence holds the mouse input while it walks Marlene around - the Raycaster
        /// swallows every click as long as one is running. SequenceRunner rides on the Global prefab,
        /// so a sequence the visitor walked out on keeps its coroutine, and its input lock, right
        /// across the scene swap into the next play-through.
        /// </summary>
        private void AbortRunningSequence()
        {
            if (sequenceRunner == null)
                sequenceRunner = FindFirstObjectByType<SequenceRunner>(FindObjectsInactive.Include);

            if (sequenceRunner == null)
            {
                Debug.LogWarning("GameResetter: no SequenceRunner found, a running sequence keeps the input.");
                return;
            }

            sequenceRunner.AbortSequence();
        }

        /// <summary>
        /// A dialog the visitor walked out on keeps running. DialogTreeRunner rides on the Global
        /// prefab, so its coroutines, its subtitle line and its options all survive the scene swap,
        /// and Raycaster.isDialogRunning stays up - which makes the Raycaster ignore the mouse for
        /// the whole of the next visit. Reset is the runner's own teardown: it stops the coroutines,
        /// hides the dialog UI and posts the running-status change everybody else listens for.
        /// </summary>
        private void AbortRunningDialog()
        {
            if (dialogTreeRunner == null)
                dialogTreeRunner = FindFirstObjectByType<DialogTreeRunner>(FindObjectsInactive.Include);

            if (dialogTreeRunner == null)
            {
                Debug.LogWarning("GameResetter: no DialogTreeRunner found, a running dialog keeps the input.");
                return;
            }

            // Reset walks the receiver lists DialogBuilderHQ hands over in Start, and those stay null
            // when a scene holds no dialog clients at all. A dialog that is running proves they are
            // there - and without one there is nothing to abort anyway.
            if (dialogTreeRunner.IsDialogRunning)
                dialogTreeRunner.Reset();
        }

        /// <summary>
        /// The Raycaster sits on the Global prefab and implements no ISceneSetupCallbackReceiver, so
        /// SceneSetup.SetupScene() - the reset for everything else - never reaches it. What it keeps
        /// are the flags that gate the mouse (a menu the visitor left open, a dialog that was
        /// running) and the outline of whatever was hovered last; ResetState drops all of it and puts
        /// the cursor back on its standard symbol.
        /// </summary>
        private void ResetInputState()
        {
            if (raycaster == null)
                raycaster = FindFirstObjectByType<Raycaster>(FindObjectsInactive.Include);

            if (raycaster == null)
            {
                Debug.LogWarning("GameResetter: no Raycaster found, a menu or dialog flag keeps the mouse.");
                return;
            }

            raycaster.ResetState();
        }

        /// <summary>
        /// Puts Marlene back on the pose that stands in the Global prefab. She rides on that prefab,
        /// which is DontDestroyOnLoad, so nothing about a scene load moves her on its own - the next
        /// visitor would start wherever the last one left her.
        ///
        /// The NavMeshAgent is the part that has to be convinced. It carries its own position and,
        /// with updatePosition on, writes that back onto the transform every frame it spends on a
        /// NavMesh - so setting the transform alone holds only until the next scene brings a NavMesh
        /// with it. Warp is the usual way to resync the two, but it needs a loaded NavMesh to warp
        /// onto and the first scene has none: the only NavMeshSurface in the game sits in Scene 2.
        /// Switching the agent off throws its position away, and switching it back on seeds a fresh
        /// one from the transform we just corrected, so Scene 2's NavMesh picks Marlene up at the
        /// start pose rather than where the last play-through ended.
        ///
        /// This runs once the first scene is back, not before the swap: the start position belongs
        /// to the first scene rather than to the one the visitor abandoned.
        /// </summary>
        private void MovePlayerToStart()
        {
            ReadStartPose();

            if (!_startPoseKnown || !FindPlayer())
                return;

            var marlene = player.transform;
            var agent = player.GetComponent<NavMeshAgent>();
            var body = player.GetComponent<Rigidbody>();

            // Off and on again inside the same call: nothing runs in between, so MoveByClick never
            // gets a frame in which it reads pathPending off a disabled agent.
            var agentReseeded = agent != null && agent.enabled;

            if (agentReseeded)
                agent.enabled = false;

            var bodyWasKinematic = false;
            var bodyInterpolation = RigidbodyInterpolation.None;

            if (body != null)
            {
                bodyWasKinematic = body.isKinematic;
                bodyInterpolation = body.interpolation;

                // The Rigidbody is the second owner of this transform, and on its own it undoes the
                // write: Marlene's body is non-kinematic and interpolated, so every frame Unity
                // rewrites the transform from the body's own pose - and with
                // Physics.autoSyncTransforms off project-wide (DynamicsManager) a bare transform
                // write never reaches that pose to begin with. Kinematic and un-interpolated for
                // the length of the teleport takes the body out of the argument.
                body.interpolation = RigidbodyInterpolation.None;
                body.isKinematic = true;
            }

            marlene.localPosition = _startLocalPosition;
            marlene.localRotation = _startLocalRotation;

            if (body != null)
            {
                // position/rotation, not MovePosition: this is a teleport, and MovePosition would
                // have her walk the whole way back from where the last visitor left her.
                body.position = marlene.position;
                body.rotation = marlene.rotation;

                body.isKinematic = bodyWasKinematic;
                body.interpolation = bodyInterpolation;

                if (!bodyWasKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            // Only now the agent, so it seeds from the corrected transform.
            if (agentReseeded)
                agent.enabled = true;

            // autoSyncTransforms is off, so without this the physics scene keeps the old pose until
            // the next simulation step and anything raycasting in between finds Marlene where she was.
            Physics.SyncTransforms();

            // Left unconditional on purpose: a reset happens once per visitor, and this one line in
            // the Editor log is what proves where Marlene actually ended up.
            Debug.Log($"GameResetter: Marlene reset to local {marlene.localPosition} / " +
                      $"world {marlene.position}, agent re-seeded: {agentReseeded}, " +
                      $"body: {(body == null ? "none" : bodyWasKinematic ? "kinematic" : "dynamic")}");
        }

        /// <summary>
        /// Read out of the prefab asset itself - the same one the Bootstrapper spawns - so it is the
        /// value that stands in Global, no matter what the finished play-through did to the live
        /// object or how late this is called.
        /// </summary>
        private void ReadStartPose()
        {
            if (_startPoseKnown)
                return;

            var prefab = Resources.Load<GameObject>("Prefabs/Global");
            var prefabPlayer = prefab != null ? prefab.GetComponentInChildren<PlayerController>(true) : null;

            if (prefabPlayer == null)
            {
                Debug.LogWarning("GameResetter: no PlayerController under Resources/Prefabs/Global, " +
                                 "Marlene keeps her position.");
                return;
            }

            _startLocalPosition = prefabPlayer.transform.localPosition;
            _startLocalRotation = prefabPlayer.transform.localRotation;
            _startPoseKnown = true;
        }

        private bool FindPlayer()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

            if (player != null)
                return true;

            Debug.LogWarning("GameResetter: no PlayerController found, Marlene keeps her position.");
            return false;
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

            MovePlayerToStart();

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
