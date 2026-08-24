using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Markiert eine begehbare Fläche als drinnen oder draußen. <see cref="AnimationSoundTrigger"/>
    /// schießt beim Schritt einen Strahl nach unten auf die Layer "Scene Plane" und liest die
    /// Markierung von dem, was er trifft — ohne Marker gilt <see cref="FootstepSurface.Outdoor"/>.
    ///
    /// Gehört auf die vier Planes unter "Locations" in Scene 2: "Treppe Plane", "Terasse Plane"
    /// und "Haus Plane" sind Drinnen (Holz), "Scene Plane" bleibt ohne Marker (Kies). Damit
    /// beginnt Drinnen an der Treppe zur Veranda, so wie gewünscht.
    ///
    /// Bewusst nicht an die State-Gruppe SCENE02_location gehängt: die schaltet an der Hauswand
    /// (RoomTrigger "Trigger Garten", Z ab -2.33) und entscheidet über Raumton gegen Vögel. Die
    /// Veranda ist für die Ambience draußen, für die Schritte aber Holz. Zwei Grenzen, zwei Game
    /// Syncs — sonst überschreibt der Schritt-Sound die Ambience.
    /// </summary>
    public class FootstepSurfaceMarker : MonoBehaviour
    {
        [Tooltip("Welcher Untergrund hier zu hören sein soll.")]
        [SerializeField] private FootstepSurface surface = FootstepSurface.Indoor;

        public FootstepSurface Surface => surface;
    }

    public enum FootstepSurface
    {
        /// <summary>Kies — FTS_Surface > Pebbles.</summary>
        Outdoor = 0,

        /// <summary>Holz — FTS_Surface > Wood.</summary>
        Indoor = 1,
    }
}
