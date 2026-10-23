using System;
using DolocTown;
using HarmonyLib;

namespace Yinch.CelesteMovement;

[HarmonyPatch(typeof(BodyController), nameof(BodyController.Update))]
internal static class UpdatePatch
{
    private static void Prefix(BodyController __instance)
    {
        try { MovementRuntime.UpdateFreeze(); MovementRuntime.Synchronize(__instance); }
        catch (Exception error) { MovementRuntime.Fault(error); }
    }
    private static void Postfix(BodyController __instance)
    {
        MovementRuntime.Current(__instance)?.FrameUpdate();
    }
}

[HarmonyPatch(typeof(BodyController), nameof(BodyController.FixedUpdate))]
internal static class PhysicsBoundaryPatch
{
    private static void Prefix(BodyController __instance)
    {
        try { if (MovementRuntime.Current(__instance) != null && !MovementRuntime.Eligible(__instance)) MovementRuntime.Release(); }
        catch (Exception error) { MovementRuntime.Fault(error); }
    }
}

[HarmonyPatch(typeof(AgentControllerState), "AgentMotion")]
internal static class InputPatch
{
    private static bool Prefix(BodyController ___body, ref bool __result)
    {
        try
        {
            MovementRuntime.Synchronize(___body);
            var state = MovementRuntime.Current(___body);
            if (state == null || !MovementRuntime.Eligible(___body)) return true;
            state.Sample();
            ___body.CheckJumpDownReleased(___body.UserInput.NormalJumpDownReleased);
            // Preserve the native platform/portal exit branch when its dedicated binding is used.
            if (___body.UserInput.NormalJumpDown && !state.MotionPressed &&
                !___body.UserInput.NormalJump && !___body.UserInput.NormalDash) return true;
            __result = state.MotionPressed;
            return false;
        }
        catch (Exception error) { MovementRuntime.Fault(error); return true; }
    }
}

[HarmonyPatch(typeof(BodyController), nameof(BodyController.JumpDown))]
internal static class DropThroughPatch
{
    private static bool Prefix(BodyController __instance, ref bool __result)
    {
        try
        {
            var state = MovementRuntime.Current(__instance);
            // Down is crouch/fast-fall in Celeste. Returning false still lets
            // AgentMotion try a native door exit, without dropping through floors.
            if(state!=null) { __result=false; return false; }
        }
        catch (Exception error) { MovementRuntime.Fault(error); }
        return true;
    }
}

[HarmonyPatch(typeof(BodyController), "set_Pause")]
internal static class PausePatch
{
    private static void Prefix(BodyController __instance, bool value)
    {
        try { if(value) MovementRuntime.Suspend(__instance); }
        catch(Exception error) { MovementRuntime.Fault(error); }
    }
}

[HarmonyPatch(typeof(BodyController), "set_InCutscene")]
internal static class CutsceneHandoffPatch
{
    private static void Prefix(BodyController __instance, bool value)
    {
        try
        {
            if(!value) return;
            var state=MovementRuntime.Current(__instance);
            if(state==null) return;
            // Native setter sets isKinematic before exiting the old agent state.
            // Restore our physics first, so OnExit cannot undo that native write.
            state.Suspended=false;
            MovementRuntime.Release(false);
        }
        catch(Exception error) { MovementRuntime.Fault(error); }
    }
    private static void Postfix(BodyController __instance, bool value)
    {
        if(__instance!=DolocAPI.agent||MovementRuntime.Config?.DiagnosticLogging!=true) return;
        var rb=(UnityEngine.Rigidbody2D)__instance.GetComponent(typeof(UnityEngine.Rigidbody2D));
        MovementRuntime.Monitor?.Log($"Cutscene handoff active={value}; bodyType={rb?.bodyType}; nativeState={__instance.StateManager?.current?.GetType().Name}");
    }
}

[HarmonyPatch(typeof(BodyController), "get_Velocity")]
internal static class VelocityPatch
{
    private static void Postfix(BodyController __instance, ref UnityEngine.Vector2 __result)
    {
        var state=MovementRuntime.Current(__instance);
        if(state!=null) __result=state.LogicalVelocity;
    }
}

[HarmonyPatch(typeof(BodyController), "get_IsInAirState")]
internal static class AirStatePatch
{
    private static void Postfix(BodyController __instance, ref bool __result)
    {
        var state = MovementRuntime.Current(__instance);
        if (state != null) __result = !state.Grounded || state.Motor.Dashing;
    }
}

[HarmonyPatch(typeof(BodyController), "get_IsDash")]
internal static class DashStatePatch
{
    private static void Postfix(BodyController __instance, ref bool __result)
    {
        var state = MovementRuntime.Current(__instance);
        if (state != null) __result = state.Motor.Dashing;
    }
}

[HarmonyPatch(typeof(BodyController), nameof(BodyController.SetRoom))]
internal static class RoomPatch
{
    private static void Prefix(BodyController __instance)
    {
        if (MovementRuntime.Current(__instance) != null) MovementRuntime.Release();
    }
}

[HarmonyPatch(typeof(BodyController), "OnDisable")]
internal static class DisablePatch
{
    private static void Prefix(BodyController __instance)
    {
        if (MovementRuntime.Current(__instance) != null) MovementRuntime.Release();
    }
}
