using Runtime.Scripts.Interactables;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Runs the clock in the phone's status bar forward while the scene is up. The phone only ever
    /// shows the string it is handed, so the minutes are counted here and pushed in through
    /// <see cref="Smartphone.SetTime"/> — once per displayed minute rather than once per frame.
    ///
    /// Belongs on the Smartphone object in Scene 1. The time set on the Smartphone itself is where
    /// the clock starts, so that inspector field stays the one place deciding what a visitor first
    /// sees; <see cref="startTimeOverride"/> is only there to try another start without touching it.
    /// </summary>
    [DisallowMultipleComponent]
    public class SmartphoneClock : MonoBehaviour, ISceneSetupCallbackReceiver
    {
        private const int MinutesPerDay = 24 * 60;

        [Tooltip("Real seconds one minute on the phone takes. 60 runs the clock at real time.")]
        [SerializeField, Min(0.01f)] private float secondsPerMinute = 60f;

        [Tooltip("Starts the clock here instead of at the Smartphone's own time, as h:mm. Leave empty to use the phone's.")]
        [SerializeField] private string startTimeOverride = string.Empty;

        private Smartphone smartphone;
        private int startMinute;
        private int shownMinute = -1;
        private float elapsed;

        /// <summary>
        /// The start is read once, here: by the time the inactivity reset comes round the phone's
        /// own time is whatever this script last wrote, and reading it again would let the clock
        /// carry on from where the last visitor left it instead of starting over.
        /// </summary>
        private void Awake()
        {
            smartphone = GetComponent<Smartphone>();
            if (smartphone == null)
                smartphone = FindFirstObjectByType<Smartphone>();

            if (smartphone == null)
            {
                Debug.LogError("SmartphoneClock: no Smartphone in the scene, the status bar stays put.", this);
                return;
            }

            var source = string.IsNullOrWhiteSpace(startTimeOverride) ? smartphone.StatusBarTime : startTimeOverride;
            if (!TryParseTime(source, out startMinute))
            {
                Debug.LogError($"SmartphoneClock: \"{source}\" is not a time as h:mm, starting at 0:00.", this);
                startMinute = 0;
            }
        }

        private void Start()
        {
            Restart();
        }

        /// <summary>
        /// Runs on every scene load and on the inactivity reset (SceneSetup finds this through
        /// FindObjectsByType, so it needs no wiring). Without it the next visitor would pick the
        /// clock up wherever the last one left it.
        /// </summary>
        public void OnSceneSetup()
        {
            Restart();
        }

        private void Restart()
        {
            elapsed = 0f;
            shownMinute = -1;
            Show(startMinute);
        }

        private void Update()
        {
            if (smartphone == null) return;

            elapsed += Time.deltaTime;
            Show(startMinute + Mathf.FloorToInt(elapsed / secondsPerMinute));
        }

        /// <summary>
        /// Writes a minute of the day into the status bar, skipping the frames it did not change on.
        /// Formatted the way the status bar is written by hand — 24 hours, no leading zero on the hour.
        /// </summary>
        private void Show(int minuteOfDay)
        {
            if (smartphone == null) return;

            minuteOfDay = ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
            if (minuteOfDay == shownMinute) return;

            shownMinute = minuteOfDay;
            smartphone.SetTime($"{minuteOfDay / 60}:{minuteOfDay % 60:00}");
        }

        /// <summary>
        /// Split by hand rather than parsed as a DateTime: the hour is written without a leading
        /// zero, and which formats that matches depends on the culture, while "10" and "40" do not.
        /// </summary>
        private static bool TryParseTime(string value, out int minuteOfDay)
        {
            minuteOfDay = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var parts = value.Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0].Trim(), out var hours)) return false;
            if (!int.TryParse(parts[1].Trim(), out var minutes)) return false;
            if (hours is < 0 or > 23 || minutes is < 0 or > 59) return false;

            minuteOfDay = hours * 60 + minutes;
            return true;
        }
    }
}
