using System;
using DolocTown;
using DTMAPI.Abstractions;
using UnityEngine;
using Doloc.CelesteMovement.Core;

namespace Yinch.CelesteMovement;

internal static class MovementRuntime
{
    internal static ModConfig Config;
    internal static IMonitor Monitor;
    internal static IInputHelper Input;
    private static IDtmRuntimeContext context;
    private static CelesteLocomotionState state;
    private static bool failed;
    private static bool ownsFreeze;
    private static bool animationPreview;
    private static readonly WorldReadinessGate readiness=new WorldReadinessGate();
    private static Guid runtimeInstance;
    private static long recoveredRevision=-1;
    internal static void PreviewAnimations()
    {
        animationPreview=true; Forget();
        Monitor.Log("Animation preview queued for the next normal gameplay frame (14 seconds, visual only).");
    }
    internal static bool TakeAnimationPreview()
    {
        bool result=animationPreview; animationPreview=false; return result;
    }
    private static float freezeUntil, priorTimeScale;
    internal static void Freeze()
    {
        if (ownsFreeze || Time.timeScale<=0) return;
        priorTimeScale=Time.timeScale; freezeUntil=Time.unscaledTime+.05f;
        ownsFreeze=true; Time.timeScale=0;
    }
    internal static void UpdateFreeze()
    {
        if(ownsFreeze && Time.unscaledTime>=freezeUntil) EndFreeze();
    }
    private static void EndFreeze()
    {
        if(!ownsFreeze) return;
        ownsFreeze=false;
        if(Time.timeScale==0) Time.timeScale=priorTimeScale;
    }
    internal static void Suspend(BodyController body)
    {
        var current=Current(body);
        if(current==null) return;
        current.Suspended=true;
        Release(false);
    }

    internal static void Initialize(ModConfig config, IMonitor monitor, IInputHelper input, IDtmRuntimeContext runtimeContext)
    { Config = config; Monitor = monitor; Input=input; context=runtimeContext; failed = false; readiness.Reset(); recoveredRevision=-1; }
    internal static void Shutdown() { Config = null; Monitor = null; Input=null; context=null; state = null; }
    internal static void Forget() { Release(); state=null; readiness.Reset(); recoveredRevision=-1; }

    internal static bool Eligible(BodyController body)
    {
        // Other body instances must not reset the player's recovery observation.
        if(body==null||body!=DolocAPI.agent) return false;
        bool nativeReady=!failed && Config != null &&
        body != null && body == DolocAPI.agent && body.isActiveAndEnabled && body.Status != null &&
        body.StateManager != null && !body.StateManager.IsFixed && DolocAPI.IsDataLoaded &&
        DolocAPI.userInput != null && DolocAPI.userInput.CurrentState is NormalGameState &&
        !body.Pause && !body.InCutscene && !body.IsFaint && !DolocAPI.IsAgentRiding;
        if(context==null) return nativeReady;
        var snapshot=context.Snapshot;
        if(runtimeInstance!=snapshot.RuntimeInstanceId)
        {
            runtimeInstance=snapshot.RuntimeInstanceId; readiness.Reset(); recoveredRevision=-1;
        }
        var room=nativeReady?body.CurrentRoom:null;
        nativeReady=nativeReady&&room!=null&&room.IsRenderNow&&room.SceneHandle!=null;
        bool ready=readiness.Observe(snapshot.IsWorldReady,snapshot.Phase==DtmRuntimePhase.LoadingWorld,
            snapshot.SaveSessionEpoch,snapshot.Revision,nativeReady,room,body,Time.unscaledTime,Time.frameCount);
        if(ready&&readiness.UsedFallback&&recoveredRevision!=snapshot.Revision)
        {
            recoveredRevision=snapshot.Revision;
            Monitor?.Log($"Movement recovered after stale LoadingWorld; revision={snapshot.Revision}; room={room.RoomId}; native gameplay/render stable.",LogLevel.Warn);
        }
        return ready;
    }

    internal static CelesteLocomotionState Current(BodyController body) =>
        body != null && body.StateManager?.current is CelesteLocomotionState current ? current : null;

    internal static void Synchronize(BodyController body)
    {
        if (!Eligible(body))
        {
            if (Current(body) != null) Release();
            return;
        }
        var current = body.StateManager.current;
        if (current is CelesteLocomotionState) return;
        if (!(current is AgentStateIdle || current is AgentStateMove || current is AgentStateJump ||
              current is AgentStateDrop || current is AgentStateTouchGround)) return;
        if (state == null || state.Body != body)
        {
            Release();
            state = new CelesteLocomotionState(body.StateManager, body);
        }
        body.StateManager.Overwrite(state);
    }

    internal static void Release(bool initializeNative = true)
    {
        EndFreeze();
        var old = state;
        if (old == null || old.Body == null) return;
        old.RestorePhysics();
        if (old.Body.StateManager?.current == old)
        {
            // Let the native state resume without resetting a paused velocity cache or teleporting downwards.
            var next = new AgentStateIdle(old.Body.StateManager, old.Body);
            old.Body.StateManager.__SetState(next);
            if (initializeNative && !old.Body.Pause && old.Body.isActiveAndEnabled) next.OnEnter();
        }
    }

    internal static void Fault(Exception error)
    {
        failed = true;
        try { Release(); } catch (Exception restoreError) { Monitor?.Log("Movement restore error: " + restoreError, LogLevel.Error); }
        Monitor?.Log("Celeste Movement disabled for this session after an error: " + error, LogLevel.Error);
    }
}
