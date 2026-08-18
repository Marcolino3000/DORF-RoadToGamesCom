using UnityEngine;

/// <summary>
/// Keeps a dust volume centred in front of the camera, so the motes show up in every
/// location of the scene without one emitter per room.
/// The particles themselves simulate in world space and stay put while the volume travels -
/// only the spawn box follows. A location switch moves the camera in one jump, so the
/// systems are refilled there instead of trickling in over the next twenty seconds.
/// </summary>
public class FloatingDust : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How far in front of the camera the spawn box sits - roughly the middle of the room.")]
    [SerializeField] private float distanceInFront = 6f;
    [Tooltip("Lifts or drops the box off the camera's line of sight.")]
    [SerializeField] private float heightOffset = 0f;
    [Tooltip("A camera jump longer than this counts as a location switch and refills the volume.")]
    [SerializeField] private float teleportThreshold = 3f;

    [Header("References")]
    [Tooltip("Left empty it is looked up every frame until it is found - the camera lives on the Global prefab that the Bootstrapper spawns at runtime.")]
    [SerializeField] private Camera cam;

    private ParticleSystem[] systems;
    private Vector3 lastCameraPosition;
    private bool needsRefill = true;

    private void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>();
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
                return;
        }

        Vector3 cameraPosition = cam.transform.position;
        // yaw only: the box stays level while it lines up with the viewing direction
        transform.SetPositionAndRotation(
            cameraPosition + cam.transform.forward * distanceInFront + Vector3.up * heightOffset,
            Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));

        if (needsRefill || Vector3.Distance(cameraPosition, lastCameraPosition) > teleportThreshold)
        {
            Refill();
            needsRefill = false;
        }

        lastCameraPosition = cameraPosition;
    }

    /// <summary>
    /// Restarts the systems at the volume's new spot. Both are looping and prewarmed,
    /// so Unity simulates a full cycle on Play and the room is dusty right away.
    /// </summary>
    private void Refill()
    {
        foreach (ParticleSystem system in systems)
        {
            system.Clear(true);
            system.Play(true);
        }
    }
}
