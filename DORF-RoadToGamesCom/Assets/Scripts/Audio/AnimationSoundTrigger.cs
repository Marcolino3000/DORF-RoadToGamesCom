using Audio;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Postet Marlenes Schritt-Sound. Aufgerufen wird das aus den Animation Events der Lauf-Clips,
/// je einmal pro Fußaufsatz — deshalb hier nur ein einzelnes Event und keine Schleife: die
/// Schrittfrequenz kommt aus der Animation und passt damit automatisch zum Bild.
///
/// Sitzt deshalb auf "marlene_GO_v01", dem Objekt mit dem Animator, und nicht auf der Marlene-
/// Wurzel darüber: Unity sucht die Methode eines Animation Events ausschließlich auf dem
/// GameObject des Animators. Der NavMeshAgent hängt eine Ebene höher.
///
/// Vor dem Post wird der Untergrund bestimmt, und zwar im Moment des Schritts statt über
/// OnTriggerEnter/Exit. Zwei Gründe: die vier begehbaren Planes unter "Locations" beschreiben
/// ohnehin schon exakt, wo Marlene stehen kann — ein zusätzliches Trigger-Volumen wäre eine
/// zweite Geometrie, die jemand mitpflegen müsste. Und nach einem <see cref="NavMeshAgent.Warp"/>
/// beim Kiosk-Reset stimmt eine per Enter/Exit gepflegte Variable nicht mehr, eine Abfrage
/// unter den Füßen dagegen immer.
/// </summary>
public class AnimationSoundTrigger : MonoBehaviour
{
    [Header("Wwise Events")]
    [SerializeField] private AK.Wwise.Event stepEvent;

    [Header("Untergrund (Switch-Gruppe Surface)")]
    [Tooltip("Switch für drinnen, im Wwise-Projekt Surface > Wood.")]
    [SerializeField] private AK.Wwise.Switch indoorSwitch;

    [Tooltip("Switch für draußen, im Wwise-Projekt Surface > Pebbles.")]
    [SerializeField] private AK.Wwise.Switch outdoorSwitch;

    [Tooltip("Auf welchen Layern nach dem Boden gesucht wird. In Scene 2 ist das \"Scene Plane\" (7), " +
             "dort liegen die vier begehbaren Planes unter \"Locations\".")]
    [SerializeField] private LayerMask surfaceLayers = 1 << 7;

    [Tooltip("Wie weit über den Füßen der Strahl startet. Klein halten: eine höher liegende Plane " +
             "über Marlene würde sonst zuerst getroffen.")]
    [SerializeField] private float probeUp = 0.25f;

    [Tooltip("Wie weit von dort nach unten gesucht wird.")]
    [SerializeField] private float probeDistance = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private NavMeshAgent agent;

    // Ein Fehlschlag reicht als Meldung. Bei zwei Schritten pro Sekunde wäre alles andere eine
    // Log-Lawine, die auf der Messe niemand mehr liest.
    private bool warnedAboutMissedProbe;

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
    }

    /// <summary>Ziel der Animation Events in den Lauf-Clips.</summary>
    public void PlayStepSound()
    {
        ApplySurfaceSwitch();

        if (stepEvent != null && stepEvent.IsValid())
            stepEvent.Post(gameObject);
    }

    /// <summary>
    /// Der Switch gilt pro GameObject, nicht global — deshalb wird er auf demselben Objekt
    /// gesetzt, auf dem gleich das Event gepostet wird.
    /// </summary>
    private void ApplySurfaceSwitch()
    {
        var surfaceSwitch = ProbeSurface() == FootstepSurface.Indoor ? indoorSwitch : outdoorSwitch;

        if (surfaceSwitch == null || !surfaceSwitch.IsValid())
            return;

        surfaceSwitch.SetValue(gameObject);

        if (debugLogs)
            Debug.Log($"{nameof(AnimationSoundTrigger)}: Untergrund '{surfaceSwitch.Name}'", this);
    }

    /// <summary>
    /// Was unter den Füßen liegt. Ohne Treffer und ohne Marker gilt draußen — der Kies ist der
    /// Normalfall, Holz die Ausnahme, die ausdrücklich markiert sein muss.
    /// </summary>
    private FootstepSurface ProbeSurface()
    {
        var origin = FeetPosition() + Vector3.up * probeUp;

        if (!Physics.Raycast(origin, Vector3.down, out var hit, probeUp + probeDistance,
                surfaceLayers, QueryTriggerInteraction.Ignore))
        {
            if (!warnedAboutMissedProbe)
            {
                warnedAboutMissedProbe = true;
                Debug.LogWarning($"{nameof(AnimationSoundTrigger)}: kein Boden unter {origin} auf den " +
                                 "eingestellten Layern gefunden, Schritte klingen ab jetzt nach draußen.", this);
            }

            return FootstepSurface.Outdoor;
        }

        var marker = hit.collider.GetComponentInParent<FootstepSurfaceMarker>();

        return marker != null ? marker.Surface : FootstepSurface.Outdoor;
    }

    /// <summary>
    /// Marlenes Füße, gerechnet aus dem NavMeshAgent statt aus einem Transform. Der Agent trägt
    /// einen Base Offset von 2.5, seine Objekt-Position liegt also gut zwei Einheiten über dem
    /// Boden — und ein Strahl von dort träfe vor dem Boden unter ihr noch die höher liegende
    /// "Haus Plane". Ohne Agent bleibt nur die eigene Position.
    /// </summary>
    private Vector3 FeetPosition()
    {
        if (agent != null)
            return agent.transform.position - Vector3.up * agent.baseOffset;

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        var origin = FeetPosition() + Vector3.up * probeUp;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + Vector3.down * (probeUp + probeDistance));
    }
}
