---
phase: 3
status: pending
effort: M
blocked_by: [2]
---

# Phase 3: C# Touch + Sequence Mode

## Context Links

- Pattern: `MCPForUnity/Editor/Tools/RunTests.cs` — async `Task<object> HandleCommand()`, `TestJobManager` pattern
- Pattern: `MCPForUnity/Editor/Tools/McpForUnityToolAttribute.cs` — `RequiresPolling`, `PollAction`, `MaxPollSeconds`
- Pattern: `MCPForUnity/Editor/Helpers/Response.cs` — `PendingResponse` for polling
- Phase 2: `InputSimulationActions.cs` — mouse/keyboard injection patterns
- Phase 1: `InputTargetResolver.cs` — target resolution
- Unity API: `UnityEngine.InputSystem.EnhancedTouch`, `UnityEngine.InputSystem.LowLevel.TouchState`
- Unity API: `EditorApplication.update` for coroutine-like polling

## Overview

Implement touch simulation (tap, swipe, pinch) and the async `sequence` action that executes a list of steps with delays, reporting progress via polling.

## Key Insights

- Touch simulation with New Input System: `InputState.Change(Touchscreen.current.touches[0], new TouchState { position = pos, phase = Begin })`. Must cycle through Begin → Moved → Ended phases.
- Touch requires `EnhancedTouchSupport.Enable()` at start.
- Pinch requires two simultaneous touches (touches[0] and touches[1]).
- Legacy touch: no clean API. Can only fake through UGUI EventSystem with synthesized touch PointerEventData.
- Sequence mode: store step list + state in a static dictionary keyed by job_id (same pattern as TestJobManager). Use `EditorApplication.update` callback to advance steps. Return `PendingResponse` initially, poll with `status` action.
- Important: `InputSimulationBridge` is a Runtime MonoBehaviour for coroutine execution in Play Mode. It self-creates via `[RuntimeInitializeOnLoadMethod]` or on-demand singleton.

## Requirements

### Functional
- `touch_tap(target)`: Single touch at target — Begin → Ended.
- `touch_swipe(from_target, to_target, duration_ms?)`: Touch Begin at from, Moved interpolation, Ended at to. `duration_ms` defaults to 300.
- `touch_pinch(center_target, start_distance, end_distance, duration_ms?)`: Two touches symmetrically around center, interpolating distance. `duration_ms` defaults to 500.
- `sequence(steps[])`: Each step is `{ action, params, delay_ms? }`. Execute steps serially with optional delays. Returns `{ job_id, status: "running" }`. Poll via `status` action.
- `status(job_id)`: Returns current sequence job state — running/completed/failed, current step index, total steps.

### Non-Functional
- Touch actions require `#if UNITY_INPUT_SYSTEM` — no legacy fallback for touch (return clear error).
- Sequence uses async handler (`Task<object>`), or sync handler returning PendingResponse.
- Max sequence steps: 100 (guard against abuse).
- Max sequence duration: 60 seconds timeout.

## Architecture

```csharp
// InputSimulationTouch.cs — ~120 lines
namespace MCPForUnity.Editor.Tools.InputSimulation
{
    internal static class InputSimulationTouch
    {
#if UNITY_INPUT_SYSTEM
        internal static object TouchTap(ToolParams p) { ... }
        internal static object TouchSwipe(ToolParams p) { ... }
        internal static object TouchPinch(ToolParams p) { ... }
        
        private static void SimulateTouch(int fingerId, Vector2 pos, TouchPhase phase) { ... }
#else
        internal static object TouchTap(ToolParams p) => TouchNotSupported();
        internal static object TouchSwipe(ToolParams p) => TouchNotSupported();
        internal static object TouchPinch(ToolParams p) => TouchNotSupported();
        
        private static object TouchNotSupported() =>
            new ErrorResponse("Touch simulation requires com.unity.inputsystem package.");
#endif
    }
}

// InputSimulationBridge.cs (Runtime) — ~100 lines
namespace MCPForUnity.Runtime
{
    public class InputSimulationBridge : MonoBehaviour
    {
        private static InputSimulationBridge _instance;
        public static InputSimulationBridge Instance { get { ... } }
        
        // Coroutine-based sequence execution
        public string StartSequence(List<SequenceStep> steps) { ... }
        public SequenceJobState GetJobState(string jobId) { ... }
        
        [RuntimeInitializeOnLoadMethod]
        static void AutoCreate() { ... }
    }
}
```

Sequence lifecycle (static callback pattern — Editor calls Bridge directly):
1. `ManageInputSimulation.HandleCommand` receives `action=sequence`
2. Parses `steps[]` array, validates count <=100
3. Calls `InputSimulationBridge.StartSequence(steps)` — static method, both on main thread during Play Mode
4. Bridge.StartCoroutine internally, returns job_id
5. Returns `SuccessResponse("Sequence started", { job_id, status: "running", total_steps })`
6. Client polls with `action=status&job_id=xxx`
7. Bridge executes steps as coroutine (each step calls action handler directly), updating state dict
8. Status returns current_step, total, status (running/completed/failed/error)

## Related Code Files

### Create
- `MCPForUnity/Editor/Tools/InputSimulation/InputSimulationTouch.cs`
- `MCPForUnity/Runtime/InputSimulationBridge.cs`

### Modify
- `MCPForUnity/Editor/Tools/ManageInputSimulation.cs` — wire touch_tap/swipe/pinch/sequence/status actions

### Read (reference)
- `MCPForUnity/Editor/Tools/RunTests.cs` — async job pattern
- `MCPForUnity/Editor/Resources/Tests/` — TestJobManager pattern (for sequence job tracking)

## Implementation Steps

1. Create `InputSimulationTouch.cs`:
   - `#if UNITY_INPUT_SYSTEM` guard
   - `SimulateTouch(int fingerId, Vector2 pos, UnityEngine.InputSystem.TouchPhase phase)`:
     - Create `TouchState` struct, set `touchId`, `position`, `phase`
     - `InputState.Change(Touchscreen.current.touches[fingerId], touchState)`
   - `TouchTap(ToolParams p)`:
     - Resolve target → screen coords
     - SimulateTouch(0, pos, Began) → SimulateTouch(0, pos, Ended)
     - Return SuccessResponse
   - `TouchSwipe(ToolParams p)`:
     - Resolve from_target, to_target
     - For sync (non-sequence): Begin at from, Ended at to (instant)
     - For sequence: intermediate Moved phases handled by bridge coroutine
   - `TouchPinch(ToolParams p)`:
     - Resolve center_target
     - Two fingers: center ± (distance/2, 0)
     - Begin both → interpolate distances → End both
   - `#else` block: all methods return `TouchNotSupported()` error

2. Create `InputSimulationBridge.cs` in `MCPForUnity/Runtime/`:
   - Namespace: `MCPForUnity.Runtime`
   - Singleton MonoBehaviour with `DontDestroyOnLoad`
   - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` for auto-creation
   - Data classes:
     ```csharp
     public class SequenceStep { public string Action; public JObject Params; public int DelayMs; }
     public class SequenceJobState { public string JobId; public string Status; public int CurrentStep; public int TotalSteps; public string Error; }
     ```
   - `Dictionary<string, SequenceJobState> _jobs`
   - `StartSequence(List<SequenceStep> steps)`: generate GUID job_id, start coroutine, return job_id
   - Coroutine: iterate steps, for each: execute action via `CommandRegistry.InvokeCommandAsync()`, wait `DelayMs`, update state
   - `GetJobState(string jobId)`: return current state or null
   - Timeout: cancel coroutine after 60s

3. Wire into `ManageInputSimulation.cs`:
   - Add cases: `touch_tap`, `touch_swipe`, `touch_pinch` → `InputSimulationTouch` methods
   - Add case: `sequence` → parse steps array, validate count <=100, call bridge
   - Add case: `status` → call bridge `GetJobState`, return state
   - Note: `sequence` and `status` do NOT need `#if` guard — they dispatch to individual actions that have their own guards

4. Add `MCPForUnity.Runtime` assembly reference in Editor asmdef if not already present

## Todo

- [ ] Create `InputSimulationTouch.cs` with `#if UNITY_INPUT_SYSTEM` guard
- [ ] Implement `SimulateTouch()` helper
- [ ] Implement `TouchTap` action
- [ ] Implement `TouchSwipe` action
- [ ] Implement `TouchPinch` action
- [ ] Create `InputSimulationBridge.cs` Runtime MonoBehaviour
- [ ] Implement singleton auto-creation
- [ ] Implement `StartSequence()` with coroutine
- [ ] Implement `GetJobState()` for polling
- [ ] Implement sequence timeout (60s)
- [ ] Wire touch_tap/swipe/pinch/sequence/status into ManageInputSimulation.cs
- [ ] Check Editor asmdef references Runtime asmdef
- [ ] Verify compilation with and without Input System

## Success Criteria

- Touch actions work with New Input System Touchscreen device
- Touch actions return clear error without Input System package
- Sequence starts, returns job_id, polls status showing progress
- Sequence completes and reports success with step count
- Sequence errors caught and reported via status
- 60s timeout terminates stuck sequences

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| Touchscreen.current null in Editor | 3 | 4 | 12 | Check device exists; add virtual touchscreen via `InputSystem.AddDevice<Touchscreen>()` |
| Coroutine timing in Editor update loop | 2 | 3 | 6 | Use `WaitForSeconds` in coroutine; Play Mode guarantees game loop |
| CommandRegistry.InvokeCommandAsync from Runtime assembly | 3 | 3 | 9 | Bridge stores action+params; Editor code drives execution from update callback |
| Sequence steps reference self recursively | 2 | 3 | 6 | Disallow "sequence" as a step action |

## Timeline

Effort: M (medium) — ~4 hours. Touch is straightforward with InputState. Sequence needs careful coroutine/job management but pattern exists in RunTests.
