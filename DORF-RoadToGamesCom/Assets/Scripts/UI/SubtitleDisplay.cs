using Core;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Switches the dialog builder's subtitle container on and off. That container is exactly what
    /// DialogBuilderHQ's own "showSubtitles" flag toggles — but that flag only runs from OnValidate,
    /// so it is an authoring switch and unreachable for a visitor. This is the runtime counterpart;
    /// the value it applies lives on InGameAudioSettings.
    ///
    /// Hiding it mid-dialog is safe: DialogBuilderHQ collects its receivers with
    /// FindObjectsInactive.Include and hands them to the DialogTreeRunner once, in Start, so the
    /// runner keeps talking to the presenter — and writing into a TMP_Text on an inactive object is
    /// a plain field assignment. The current line reappears the moment subtitles go back on.
    ///
    /// Static because the presenter rides on the DontDestroyOnLoad Global prefab and therefore
    /// outlives every scene: there is nothing here worth a component of its own.
    /// </summary>
    public static class SubtitleDisplay
    {
        private static SubtitlePresenter presenter;
        private static bool missingPresenterLogged;

        public static void Apply(bool visible)
        {
            // Unity's null check also catches the destroyed leftover that a Play session with domain
            // reloading switched off would otherwise leave behind in this static.
            if (presenter == null)
                presenter = Object.FindFirstObjectByType<SubtitlePresenter>(FindObjectsInactive.Include);

            if (presenter == null)
            {
                // Once per session: this is called on every scene load, and a setup without a
                // dialog builder would fill the log with it.
                if (!missingPresenterLogged)
                {
                    missingPresenterLogged = true;
                    Debug.LogWarning("SubtitleDisplay: no SubtitlePresenter found, the subtitle " +
                                     "setting has nothing to switch.");
                }

                return;
            }

            presenter.gameObject.SetActive(visible);
        }
    }
}
