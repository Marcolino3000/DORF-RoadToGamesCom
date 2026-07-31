# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"Coming of Dorf" (COD) — a German-language 2D point-and-click adventure, in the build prepared for a **GamesCom exhibition kiosk**. The kiosk framing drives real requirements: the game must survive unattended operation, reset itself after visitor inactivity, and never strand a visitor on a broken screen.

Unity **6000.2.1f1** (Unity 6.2), URP 2D, Wwise audio, Odin Inspector.

## Repository layout

The repo root is *not* the Unity project. The Unity project is the `DORF-RoadToGamesCom/` subdirectory — open that folder in Unity, and treat paths in `.csproj`/`.sln` files as relative to it.

## Build, run, test

There is no CLI build, no test suite, and no CI. All building and running happens through the Unity Editor. `com.unity.test-framework` is in the manifest but no tests exist (`WwiseRTPCTester.cs` is a runtime RTPC debug helper, not a unit test).

Practical consequences:

- You cannot verify gameplay changes by running a command. Reason carefully about correctness, and say plainly when a change needs to be checked in the Editor.
- Only `Scene 1` and `Scene 2` are in the build settings. The other `.unity` files under `Assets/Scenes/` (`Feldweg A Terrain`, `Feldweg B`, `Vadim`, `Particle Test`, `Scene 3`, `Scene 4`) are work-in-progress or scratch scenes and do not ship.
- Wwise soundbanks are generated from the separate `Cod-Main_WwiseProject/`, not from Unity.

## Where the code actually lives

**Most gameplay logic is not in `Assets/Scripts`.** It lives in five custom UPM packages pulled from git:

| Package | Contains |
|---|---|
| `com.cod.interactionbuilder` | Interaction/reaction system, scene swapping, persistence — the core engine |
| `com.cod.dialog-builder` | Dialog tree runtime and node graph |
| `com.cod.playerinput` | `PlayerController`, `InputDispatcher`, inactivity timeout |
| `com.cod.audioplayer` | Audio playback helpers |
| `com.cod.csvmanager` | Editor-side CSV/Scapple import-export for dialog authoring |

Their sources are readable at `DORF-RoadToGamesCom/Library/PackageCache/com.cod.*@<hash>/`. **`Library/` is gitignored and regenerated** — edits there are silently lost on reimport. To change package behavior, change it in the package's own repo (all under `github.com/Marcolino3000`) and bump the dependency; do not edit `PackageCache`.

`Assets/Scripts/` (~40 files) is thin project-specific glue: scene choreography, UI menus, visual triggers, Wwise wiring. That is where project changes normally belong.

Namespaces do not track folder names. Package code uses `Runtime.Scripts.Core`, `Runtime.Scripts.Interactables`, `SceneManagement`, `Tree`, `Nodes.Decorator`, and some `DefaultNamespace`. Project code uses short namespaces (`Setup`, `ScenesSwitches`, `UI`, `Utility`) or none. Grep for the type, don't guess the using.

There are no assembly definitions for game code — everything compiles into `Assembly-CSharp`, so any script can see any other.

## Core architecture: the interaction system

Gameplay is authored as **ScriptableObject assets under `Assets/Resources/ScriptableObjects/`**, not as code. Understanding this chain is the key to being productive:

1. An `Interactable` (MonoBehaviour in a scene) fires `OnEnteredTriggerArea` / `OnInteractionStarted` / `OnExitedTriggerArea`, or a `DialogOptionNode` is selected.
2. That becomes a `Trigger` record (trigger type + the `InteractableState` or dialog option that caused it).
3. `InteractionHandler` (a `SerializedScriptableObject`) looks the `Trigger` up in two dictionaries — `triggersToPrerequisitesHighPrio`, then `triggersToPrerequisitesLowPrio`. **High priority short-circuits: if a high-prio match exists, low-prio never runs.** This is the usual reason an interaction "doesn't fire".
4. Matching `PrerequisiteRecord`s raise `OnPrerequisiteReady`.
5. An `InteractionData` asset runs its `successReaction` or `failureReaction`.
6. A `Reaction` asset executes a declarative bundle of effects — start a dialog tree, run a `ScriptedSequence` of waypoints, move/show an interactable, or load a scene — then raises `OnReactionFinished`.

`Reaction.Execute()` is a flat sequence of null checks, so one Reaction can do several of these at once. Note two quirks in it: `ObjectToMoveIn`/`ObjectToMoveOut` **`return` early** (not `continue`) when the object is already in the requested state, which aborts the rest of the reaction; and objects are moved by ±30 on Y rather than being deactivated.

Adding content usually means creating and wiring SO assets in the Editor, not writing C#.

## State lives in ScriptableObjects — the main gotcha

`InteractionData`, `InteractableState`, and other `WorldStateOwner` subclasses store **mutable runtime state** (`Count`, `ThresholdReached`, `IsRunning`, `IsActive`, `Spawned`) directly on the asset. In the Editor this state **persists across play sessions and is written to disk**, so a play-through dirties assets and the next run starts mid-progress.

The reset path is `SceneSetup.SetupScene()`, which finds every `ISceneSetupCallbackReceiver` — both scene MonoBehaviours and ScriptableObjects loaded via `Resources.LoadAll` — and calls `OnSceneSetup()`. It runs on every scene load (from `SceneSwapManager.OnSceneLoaded`) and on kiosk reset.

When touching this state: anything that must survive a scene load belongs on the SO; anything that must not must be reset in `OnSceneSetup()`.

## Startup and kiosk lifecycle

`Bootstrapper` runs at `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` and does two things before any scene loads: spawns an `AkInitializer` to boot Wwise, then instantiates `Resources/Prefabs/Global` as `DontDestroyOnLoad`. The Wwise-first ordering is deliberate and load-bearing — the `Global` prefab's `AkAudioListener`/`AkGameObj`/`AkEvent` components post from `Awake`/`OnEnable`, and in a build those calls fail permanently if the engine isn't up yet. **The Editor masks this bug** because Wwise already runs in edit mode, so audio-init regressions only show in builds.

Kiosk reset: `InputDispatcher.OnGameReset` — a serialized `UnityEvent`, so the hookup to `GameResetter.ResetGame` is wired in the Inspector, not in code — fires after an inactivity timeout. `GameResetter` then resets interactables, plays a looping attract video over the running scene, and restarts at `Scene 1` on any key/mouse/gamepad press. It deliberately no-ops while `StartSplash.IsShowing`, so the attract screen isn't double-reset.

Scene transitions go through `SceneSwapManager` (a singleton MonoBehaviour on the `Global` prefab). Prefer `PreloadAndChangeScene` over `ChangeScene` for player-visible transitions — it loads in the background with the current scene fully visible, then fades. It also waits one extra frame after loading so the inflated `Time.deltaTime` from the load frame doesn't snap the fade straight to black.

## Conventions

- **The game is German.** Content, asset names, and many identifiers are German (`Sauerteig`, `Waldsteine`, `Fußmatte`, `Bäumis`). Code comments and commit messages are mixed German and English — match the surrounding file rather than normalizing.
- Odin Inspector is used throughout (`SerializedScriptableObject`, `[InspectorButton]`, dictionary serialization). It is vendored into `Assets/Plugins/Sirenix` and committed.
- `[AutoAssign]` fields are filled by an editor-only processor on load and on project change — but **only when exactly one object of that type exists**. With zero or multiple candidates it silently does nothing, leaving the field null. A null `[AutoAssign]` reference usually means a duplicate asset, not a missing one.
- `LogManager` (a ScriptableObject) can install a filtering `ILogHandler` to suppress noisy log lines by substring, but its `OnEnable` call is currently commented out — it is inert unless `Setup()` is invoked manually.
- Unity `.meta` files must accompany every added, moved, or deleted asset. `.gitattributes` is configured for Unity YAML merging.
