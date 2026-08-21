using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Ties an Animator's playback speed to how fast the landscape is scrolling past the train
    /// window in Scene 1.
    ///
    /// Marlene's sitting loop is the carriage's motion made visible — she rocks because the train
    /// does. Playing it at its authored speed while the panorama brakes to a halt reads as the loop
    /// having nothing to do with the train, so the speed is taken from
    /// <see cref="Landscape.SpeedFactor"/> instead: calmer rocking throughout the ride, winding
    /// down over the brake, and frozen on whatever pose she is in once the landscape stands still.
    /// </summary>
    public class AnimationSpeedFromLandscape : MonoBehaviour
    {
        // Below this fraction of full scroll the rocking is too slow to read as movement anyway and
        // the last frames only creep. Snap it shut instead.
        private const float StopBelowFactor = 0.02f;

        [Header("Settings")]
        [Tooltip("Animator speed while the train is at full speed. 1 plays the clip as authored, " +
                 "lower is calmer. Scaled down from here as the landscape brakes.")]
        [Range(0.05f, 1f)] [SerializeField] private float speedWhileMoving = 0.5f;

        [Header("References")]
        [Tooltip("Left empty, the Animator on this object or below it is used.")]
        [SerializeField] private Animator animator;

        [Tooltip("Left empty, the Landscape in the scene is used.")]
        [SerializeField] private Landscape landscape;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (landscape == null)
                landscape = FindFirstObjectByType<Landscape>();

            if (animator == null)
                Debug.LogWarning($"{name}: no Animator to drive, animation speed stays as authored.",
                    this);

            if (landscape == null)
                Debug.LogWarning($"{name}: no Landscape in the scene, animation speed stays as " +
                                 "authored.", this);
        }

        private void Update()
        {
            if (animator == null || landscape == null) return;

            float factor = landscape.SpeedFactor;
            animator.speed = factor < StopBelowFactor ? 0f : speedWhileMoving * factor;
        }
    }
}
