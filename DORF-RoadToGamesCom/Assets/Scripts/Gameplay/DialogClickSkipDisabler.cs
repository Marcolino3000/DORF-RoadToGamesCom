using Tree;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Turns off the dialog runner's click-to-skip for as long as this scene is loaded.
    ///
    /// <see cref="DialogTreeRunner"/> reads <c>Input.GetMouseButtonDown(0)</c> straight from its
    /// Update and skips a dialog line on every click, without asking whether the click was meant
    /// for the scene at all. In the train the phone is the only thing to operate — open a chat, go
    /// back, close it — and each of those clicks would skip a line of the two-node
    /// MarianneSprachiDialog, whose end starts the transition to Scene 2.
    ///
    /// That Update holds nothing but the skip logic, so disabling the component is enough. Running
    /// coroutines — the dialog itself — keep going: those only stop when the GameObject is
    /// deactivated, which never happens here.
    /// </summary>
    public class DialogClickSkipDisabler : MonoBehaviour
    {
        private DialogTreeRunner runner;

        private void Awake()
        {
            // The runner sits on the Global prefab, which the Bootstrapper spawns before the first
            // scene loads — so it is already there by the time this Awake runs.
            runner = FindFirstObjectByType<DialogTreeRunner>(FindObjectsInactive.Include);

            if (runner == null)
            {
                Debug.LogWarning("DialogClickSkipDisabler: no DialogTreeRunner found, clicks still skip dialog lines.", this);
                return;
            }

            runner.enabled = false;
        }

        private void OnDestroy()
        {
            // This scene is torn down before the next one wakes up, so from Scene 2 on the dialogs
            // are click-skippable again, exactly as they are everywhere else in the game.
            if (runner != null)
                runner.enabled = true;
        }
    }
}
