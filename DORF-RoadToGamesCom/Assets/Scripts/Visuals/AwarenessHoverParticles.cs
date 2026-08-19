using System.Collections.Generic;
using Runtime.Scripts.Interactables;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fires two one-shots whenever the mouse moves onto an interactable at
/// <see cref="minimumAwarenessLevel"/> or above, while the Sauerteig is unlocked:
/// <see cref="sauerteigParticles"/> in place on this object, and
/// <see cref="interactableParticles"/> teleported onto the interactable.
/// The hover test mirrors the Raycaster's own one (walls and Marlene block, the ground does not),
/// so the shots line up with the special outline.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class AwarenessHoverParticles : MonoBehaviour, ISceneSetupCallbackReceiver
{
    [Header("Settings")]
    [Tooltip("Hovering an interactable at this AwarenessLevel or higher fires one shot.")]
    [SerializeField] private AwarenessLevel minimumAwarenessLevel = AwarenessLevel.Super;
    [Tooltip("World-space nudge off the interactable's pivot, e.g. up to its visual centre.")]
    [SerializeField] private Vector3 emitOffset;
    [Tooltip("Pulls the spawn point this far towards the camera, so the particles sit in front of the interactable's sprite instead of inside it.")]
    [SerializeField] private float distanceTowardsCamera = 0.5f;
    [Tooltip("Defaults mirror the Raycaster on the Global prefab: it keeps hovering during dialog, but not during sequences.")]
    [SerializeField] private bool suppressDuringDialog;
    [SerializeField] private bool suppressDuringSequences = true;

    [Header("References")]
    [Tooltip("Stays where it is and fires in place - it hangs off Marlene, so it is the Sauerteig's own effect.")]
    [SerializeField] private ParticleSystem sauerteigParticles;
    [Tooltip("Lives in the scene, not on the Global prefab, so it usually cannot be assigned here. Left empty it is looked up by name once per scene load.")]
    [SerializeField] private ParticleSystem interactableParticles;
    [SerializeField] private string interactableParticlesName = "InteractableParticles";
    [Tooltip("These three sit on the Global prefab, which the Bootstrapper spawns from Resources at runtime - so they can only be assigned here if this component sits on that prefab too. Left empty they are looked up once, at Awake.")]
    [SerializeField] private Camera cam;
    [SerializeField] private Sauerteig sauerteig;
    [SerializeField] private Raycaster raycaster;

    // read these in the inspector while playing: they show which gate is stopping the shot
    [Header("Debug")]
    [SerializeField] private bool sauerteigUnlocked;
    [SerializeField] private bool inputSuppressed;
    [SerializeField] private Interactable interactableUnderMouse;
    [SerializeField] private AwarenessLevel levelUnderMouse;
    [SerializeField] private ParticleSystem resolvedInteractableParticles;
    [SerializeField] private int shotsFired;

    private bool searchedForInteractableParticles;
    private Interactable lastQualifying;
    private int wallLayer;
    private int groundLayer;
    private int playerLayer;
    private int hoverLayerMask;
    private readonly List<Hit> hits = new List<Hit>();

    private void Awake()
    {
        if (sauerteigParticles == null)
            sauerteigParticles = GetComponent<ParticleSystem>();

        // it hangs off Marlene, so local space is what carries the particles along with her once
        // they have spawned - pinned here so a stray inspector change cannot break it
        var main = sauerteigParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        MakeOneShot(sauerteigParticles);

        wallLayer = LayerMask.NameToLayer("Walls");
        groundLayer = LayerMask.NameToLayer("Scene Plane");
        playerLayer = LayerMask.NameToLayer("Marlene");
        hoverLayerMask = LayerMask.GetMask("Interactables", "Scene Plane", "Walls", "Marlene");

        // The Bootstrapper instantiates Global before the first scene loads, so by Awake it is
        // there. One lookup covers the case where this component cannot reference it directly.
        if (cam == null)
            cam = Camera.main;

        if (sauerteig == null)
            sauerteig = FindFirstObjectByType<Sauerteig>();

        if (raycaster == null)
            raycaster = FindFirstObjectByType<Raycaster>();

        if (cam == null || sauerteig == null)
        {
            Debug.LogError($"{nameof(AwarenessHoverParticles)} on '{name}' found no {(cam == null ? "Camera" : nameof(Sauerteig))}.", this);
            enabled = false;
            return;
        }

        if (raycaster == null)
            Debug.LogWarning($"{nameof(AwarenessHoverParticles)} on '{name}' found no {nameof(Raycaster)}; menu and sequence states are ignored.", this);
    }

    private void Update()
    {
        var qualifying = GetQualifyingInteractable();

        if (qualifying == lastQualifying)
            return;

        lastQualifying = qualifying;

        // fires once per interactable the mouse moves onto; moving away lets the shot finish
        if (qualifying != null)
            EmitOneShot(GetEmitPosition(qualifying));
    }

    private Vector3 GetEmitPosition(Interactable interactable)
    {
        Vector3 position = interactable.transform.position + emitOffset;

        // the camera is perspective and driven by Cinemachine, so aim at where it actually is
        // rather than along a fixed axis
        Vector3 towardsCamera = cam.transform.position - position;

        if (towardsCamera.sqrMagnitude > Mathf.Epsilon)
            position += towardsCamera.normalized * distanceTowardsCamera;

        return position;
    }

    // runs on every scene load, so this is where the scene-side spawner is dropped and re-found
    public void OnSceneSetup()
    {
        lastQualifying = null;
        searchedForInteractableParticles = false;
        resolvedInteractableParticles = null;

        Clear(sauerteigParticles);

        // resolve straight away rather than on the first shot, so the debug field and any warning
        // show up at scene load
        GetInteractableParticles();
    }

    private void EmitOneShot(Vector3 position)
    {
        // the Sauerteig's own puff fires where it hangs, on Marlene
        PlayOneShot(sauerteigParticles);

        var atInteractable = GetInteractableParticles();

        if (atInteractable != null)
        {
            atInteractable.transform.position = position;
            PlayOneShot(atInteractable);
        }

        shotsFired++;
    }

    // The Global prefab is spawned before the first scene loads, so this cannot be serialized
    // against a scene object. Resolved lazily and cached until the next scene load.
    private ParticleSystem GetInteractableParticles()
    {
        if (interactableParticles != null)
            return interactableParticles;

        if (searchedForInteractableParticles)
            return resolvedInteractableParticles;

        searchedForInteractableParticles = true;
        resolvedInteractableParticles = FindByName() ?? FindOnlyOneInLoadedScene();

        if (resolvedInteractableParticles == null)
            return null;

        // it gets teleported onto each interactable, so world space leaves the particles of an
        // earlier shot where they were emitted instead of dragging them along
        var main = resolvedInteractableParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        MakeOneShot(resolvedInteractableParticles);

        return resolvedInteractableParticles;
    }

    private ParticleSystem FindByName()
    {
        if (string.IsNullOrEmpty(interactableParticlesName))
            return null;

        var spawner = GameObject.Find(interactableParticlesName);

        return spawner != null ? spawner.GetComponent<ParticleSystem>() : null;
    }

    // Fallback for when the object is not named as expected: take the single particle system that
    // belongs to the loaded scene rather than to Global. Same rule as [AutoAssign] - with none or
    // several candidates it stays out of the way and says so.
    private ParticleSystem FindOnlyOneInLoadedScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        ParticleSystem candidate = null;
        var candidateCount = 0;

        foreach (var system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (system == sauerteigParticles || system.gameObject.scene != activeScene)
                continue;

            candidate = system;
            candidateCount++;
        }

        if (candidateCount == 1)
            return candidate;

        Debug.LogWarning($"{nameof(AwarenessHoverParticles)}: no active GameObject '{interactableParticlesName}' with a {nameof(ParticleSystem)}, and {candidateCount} other candidates in '{activeScene.name}' - rename the spawner or assign it explicitly.", this);

        return null;
    }

    private static void MakeOneShot(ParticleSystem system)
    {
        // one shot per hover: no loop, and nothing at scene start
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;

        Clear(system);
    }

    private static void PlayOneShot(ParticleSystem system)
    {
        if (system == null)
            return;

        // restart the cycle without clearing the particles from a previous shot
        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        system.Play();
    }

    private static void Clear(ParticleSystem system)
    {
        if (system != null)
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Interactable GetQualifyingInteractable()
    {
        // every gate writes its debug field before bailing out, so the inspector always shows how
        // far the check got this frame
        if (cam == null)
            return null;

        sauerteigUnlocked = sauerteig.IsUnlocked;

        inputSuppressed = raycaster != null &&
                          (raycaster.IsMenuOpen ||
                           (suppressDuringDialog && raycaster.isDialogRunning) ||
                           (suppressDuringSequences && raycaster.isSequenceRunning));

        interactableUnderMouse = GetHoveredInteractable();
        levelUnderMouse = interactableUnderMouse != null && interactableUnderMouse.Data != null
            ? interactableUnderMouse.Data.AwarenessLevel
            : AwarenessLevel.NotSet;

        if (!sauerteigUnlocked || inputSuppressed)
            return null;

        if (interactableUnderMouse == null || interactableUnderMouse.Data == null)
            return null;

        return levelUnderMouse >= minimumAwarenessLevel ? interactableUnderMouse : null;
    }

    private Interactable GetHoveredInteractable()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        hits.Clear();

        foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity, hoverLayerMask))
            hits.Add(new Hit(hit.collider.gameObject, hit.distance));

        foreach (var hit in Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity))
            hits.Add(new Hit(hit.collider.gameObject, hit.distance));

        hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var hit in hits)
        {
            // walls and Marlene block the view
            if (hit.Target.layer == wallLayer || hit.Target.layer == playerLayer)
                return null;

            // the ground sits in front of the interactable's collider, so keep scanning past it
            if (hit.Target.layer == groundLayer)
                continue;

            var interactable = hit.Target.GetComponentInChildren<Interactable>();

            if (interactable == null)
                continue;

            // a trigger area carries its own collider and must not count as a hover
            if (interactable.gameObject.name.ToLower().Contains("trigger"))
                continue;

            return interactable;
        }

        return null;
    }

    private readonly struct Hit
    {
        public readonly GameObject Target;
        public readonly float Distance;

        public Hit(GameObject target, float distance)
        {
            Target = target;
            Distance = distance;
        }
    }
}
