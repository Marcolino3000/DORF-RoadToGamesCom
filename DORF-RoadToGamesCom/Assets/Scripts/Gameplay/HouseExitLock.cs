using Runtime.Scripts.Core;
using Runtime.Scripts.PlayerInput;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Ab dem Ende von Pauls erstem Dialog ist Marlene im Haus eingesperrt: ein NavMeshObstacle
/// schneidet die Haustür aus dem NavMesh, damit kein Klick sie mehr nach draußen schicken kann.
///
/// Warum am NavMesh und nicht am Klick: der Gartenboden (`Scene Plane`) hängt direkt unter
/// `Locations` und ist deshalb auch dann aktiv und anklickbar, wenn `RoomManager` den ganzen
/// `Garden` abgeschaltet hat. Der Klick-Blocker in der Türöffnung (`PhysicsCollider Tuer`) liegt
/// dagegen unter `Garden`, ist drinnen also aus - und er könnte von innen ohnehin nichts
/// ausrichten: die Kamera steht vor dem Haus, ein Strahl auf den Garten trifft den Boden
/// draußen, bevor er die Türebene erreicht. Nur eine Lücke im NavMesh hält Marlene zuverlässig
/// drin, egal wohin geklickt wird.
///
/// Die Ausnahme ist Hildes Klopf-Dialog: dessen Dialogoption startet `OpenDoorHildeSequence`,
/// und für jede ScriptedSequence ist die Sperre offen (siehe <see cref="ShouldLock"/>).
/// </summary>
public class HouseExitLock : MonoBehaviour
{
    /// <summary>
    /// Die Türöffnung aus der Wand von Scene 2, in Weltkoordinaten: `PhysicsCollider left` endet
    /// bei x -2.47, `PhysicsCollider right` fängt bei x -4.18 an, die Wand liegt bei z -1.97..-1.78.
    /// Die Box greift seitlich in die massiven Wandstücke hinein - dort liegt kein NavMesh, also
    /// kostet das nichts und die Lücke ist sicher zu. In y deckt sie die ganze Wandhöhe ab, damit
    /// die genaue Bodenhöhe an der Schwelle keine Rolle spielt.
    /// </summary>
    private static readonly Vector3 DoorwayCenter = new Vector3(-3.325f, 6f, -1.875f);
    private static readonly Vector3 DoorwaySize = new Vector3(2f, 14f, 0.5f);

    /// <summary>
    /// Drinnen fängt hinter der Wandebene (z -1.875) an. Die Schwelle liegt bewusst hinter dem
    /// Ausschnitt: mit Carving kommt Marlene nur bis z -1.125 an die Tür heran (Kante -1.625 plus
    /// Agentenradius 0.5). Läge die Schwelle davor, ginge die Sperre genau dann auf, wenn sie vor
    /// der Tür steht - und sie könnte im nächsten Klick raus.
    /// </summary>
    private const float InsideThresholdZ = -1.5f;

    private const string PaulFirstDialogAssetName = "Scene 2 - FirstDialogWithPaul";
    private const string DialogInteractionsResourcePath = "ScriptableObjects/InteractionData/Dialog";

    [Header("Debug")]
    [SerializeField] private bool isLocked;
    [SerializeField] private bool sequenceIsRunning;

    private InteractionData paulFirstDialog;
    private PlayerController player;
    private NavMeshObstacle doorway;

    /// <summary>
    /// Baut sich selbst in die Szene, statt als Komponente in der Scene-YAML zu hängen - so wie
    /// StartSplash, GameResetter und SceneIntroVignette ihre Objekte auch selbst bauen.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// Deckt die Szene ab, mit der das Spiel startet. Wer im Editor direkt in Scene 2 auf Play
    /// drückt, bekommt für diese Szene kein verlässliches sceneLoaded - dann gäbe es die Sperre
    /// im Test nicht, im echten Durchlauf über Scene 1 aber schon. Genau die Sorte Unterschied,
    /// die man erst am Messestand merkt.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInStartScene()
    {
        TryInstall();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        TryInstall();
    }

    private static void TryInstall()
    {
        // Nur die Szene mit dem Haus. Der RoomManager ist das Kennzeichen - es gibt ihn genau
        // einmal im Projekt, in Scene 2.
        if (FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include) == null)
            return;

        if (FindFirstObjectByType<HouseExitLock>(FindObjectsInactive.Include) != null)
            return;

        new GameObject(nameof(HouseExitLock)).AddComponent<HouseExitLock>();
    }

    private void Awake()
    {
        paulFirstDialog = FindDialogInteraction(PaulFirstDialogAssetName);

        // Ohne die Interaction weiß niemand, wann Pauls Dialog durch ist. Dann bleibt die Tür
        // offen: ein Besucher, der durch den Garten läuft, ist harmlos - einer, der ohne Grund
        // eingesperrt wird, steht fest.
        if (paulFirstDialog == null)
            Debug.LogWarning($"{nameof(HouseExitLock)}: '{PaulFirstDialogAssetName}' nicht gefunden, " +
                             "die Haustür bleibt unverschlossen.", this);

        CreateDoorwayObstacle();

        Debug.Log($"{nameof(HouseExitLock)}: aktiv, Pauls erster Dialog " +
                  $"{(paulFirstDialog != null ? "gefunden" : "NICHT gefunden")}.", this);
    }

    private void OnEnable()
    {
        SequenceRunner.OnSequenceRunningChanged -= HandleSequenceRunningChanged;
        SequenceRunner.OnSequenceRunningChanged += HandleSequenceRunningChanged;
    }

    // Das Event ist statisch und der SequenceRunner überlebt auf dem Global-Prefab jeden
    // Szenenwechsel - ohne das Abmelden hinge dieses Szenenobjekt für den Rest des Tages daran.
    private void OnDisable()
    {
        SequenceRunner.OnSequenceRunningChanged -= HandleSequenceRunningChanged;
    }

    /// <summary>
    /// Schaltet sofort um, nicht erst im nächsten <see cref="Update"/>: der SequenceRunner meldet
    /// den Start noch im selben Aufruf, in dem seine Coroutine schon das erste Ziel setzt
    /// (StartMovingPlayer → BeginSequence → MoveByClick.SetDestination, alles vor dem ersten yield).
    /// Ein Frame Verzögerung würde diesen SetDestination gegen das noch ausgeschnittene NavMesh
    /// rechnen - der Weg wäre nur ein Teilstück, und die Sequenz stünde bis zum waypointTimeout.
    /// Heute fällt das nicht auf, weil Pauls und Hildes Sequenzen tief im Haus anfangen und die Tür
    /// erst beim dritten Wegpunkt erreichen. Eine Sequenz, die direkt durch die Tür startet, würde
    /// darüber stolpern.
    /// </summary>
    private void HandleSequenceRunningChanged(bool isRunning)
    {
        sequenceIsRunning = isRunning;

        ApplyLock();
    }

    private void Update()
    {
        ApplyLock();
    }

    private void ApplyLock()
    {
        if (doorway == null)
            return;

        isLocked = ShouldLock();

        if (doorway.enabled == isLocked)
            return;

        doorway.enabled = isLocked;

        // Eine Zeile pro Wechsel, nicht pro Frame: ohne die ist im Editor-Log nicht zu sehen,
        // ob die Sperre überhaupt greift oder nur eine Bedingung still danebenliegt.
        Debug.Log($"{nameof(HouseExitLock)}: Haustür {(isLocked ? "gesperrt" : "frei")} " +
                  $"(Paul durch: {paulFirstDialog != null && paulFirstDialog.ThresholdReached}, " +
                  $"Sequenz läuft: {sequenceIsRunning}, " +
                  $"Marlene z: {(player != null ? player.transform.position.z : float.NaN):F2})");
    }

    /// <summary>
    /// Bewusst nur zwei Bedingungen, und die zweite ist gemessen statt gemerkt.
    ///
    /// `Portal.ToggleState` stand hier mal als "Tür offen heißt Sequenz hat sie geöffnet". Das war
    /// falsch: ToggleDoorReaction schaltet blind um, und beim Szenenladen steht die Tür wieder auf
    /// zu. Wer einen Türschritt überspringt - im Test etwa Paul direkt anklicken statt ihn klopfen
    /// zu lassen - verzählt den Schalter um eins, und die Sperre ging nie an.
    ///
    /// `RoomManager.IsOutside` hing genauso in der Luft: es kommt aus einem OnTriggerEnter, und der
    /// SequenceRunner schaltet Marlenes Collider für jeden Skriptlauf ab. Ein verschlucktes Event
    /// hätte die Sperre still ausgeschaltet gelassen.
    /// </summary>
    private bool ShouldLock()
    {
        // Vorher darf sie raus wie bisher - bis dahin antwortet die Haustür ohnehin mit
        // WontGoOutsideDialog.
        if (paulFirstDialog == null || !paulFirstDialog.ThresholdReached)
            return false;

        // Jede ScriptedSequence darf durch die Tür: OpenDoorHildeSequence aus dem Klopf-Dialog,
        // OpenDoorPaulSequence und das Zuknallen danach. Sequenzen gehen am Raycaster vorbei,
        // deshalb ist das die einzige Stelle, an der sie hier vorkommen.
        if (sequenceIsRunning)
            return false;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        // Ohne Marlene lieber offen lassen als jemanden einsperren.
        if (player == null)
            return false;

        // Draußen würde die Sperre sie aussperren statt einsperren.
        return player.transform.position.z > InsideThresholdZ;
    }

    private void CreateDoorwayObstacle()
    {
        var host = new GameObject("Doorway Obstacle");

        // Deaktiviert aufbauen: ein frisches NavMeshObstacle kommt als Kapsel und würde den Agenten
        // für einen Frame an der Türschwelle abdrängen, bevor die Einstellungen stehen.
        host.SetActive(false);
        host.transform.SetParent(transform, false);
        host.transform.position = DoorwayCenter;

        doorway = host.AddComponent<NavMeshObstacle>();
        doorway.shape = NavMeshObstacleShape.Box;
        doorway.center = Vector3.zero;
        doorway.size = DoorwaySize;
        doorway.carving = true;

        // Das Obstacle bewegt sich nie. Ohne das hier wartet das Carving erst timeToStationary ab,
        // und in dieser halben Sekunde steht die Tür noch offen.
        doorway.carveOnlyStationary = false;
        doorway.enabled = false;

        host.SetActive(true);
    }

    /// <summary>
    /// Sucht die Interaction über ihren Namen statt über einen Resources-Pfad: die Assets in
    /// diesem Ordner tragen teils geschützte Leerzeichen (U+00A0) im Namen, an denen ein
    /// geschriebener Pfad still vorbeiläuft.
    /// </summary>
    private static InteractionData FindDialogInteraction(string assetName)
    {
        foreach (var interaction in Resources.LoadAll<InteractionData>(DialogInteractionsResourcePath))
        {
            if (interaction.name.Replace('\u00A0', ' ') == assetName)
                return interaction;
        }

        return null;
    }
}
