using System;
using Doloc.CelesteMovement.Core;
using DolocTown;
using DolocTown.GameData;
using UnityEngine;

namespace Yinch.CelesteMovement;

internal sealed class CelesteLocomotionState : AgentStateBase
{
    internal BodyController Body => body;
    internal readonly PixelPlayer Motor=new PixelPlayer();
    private readonly Rigidbody2D rb;
    private readonly BoxCollider2D collider;
    private UnityPixelWorld world;
    private float scale, oldGravity, oldDrag, accumulator, logTimer, sampleTime=-100, shadowTimer;
    private RigidbodyType2D oldType;
    private CollisionDetectionMode2D oldCollision;
    private Vector2 oldSize, oldOffset, feetOffset;
    private bool ownsPhysics, initialized, jumpPending, dashPending, demoPending, dropPending;
    private bool waitingForClearance;
    private int lastInputFrame=-1;
    private PlayerInput input;
    internal bool MotionPressed { get; private set; }
    private readonly KeyboardAxis horizontal=new KeyboardAxis(), vertical=new KeyboardAxis();
    private MovementAnimation animation;
    private string probeMode="";
    private CrouchVisual crouchVisual;
    private ClimbVisual climbVisual;
    private StatusVisual statusVisual;
    internal AnimationMotion Presentation => animation?.Motion;
    internal StatusFeedback Feedback => animation?.Feedback;
    internal bool HasNativeCrouchPose => animation!=null&&animation.HasCrouchPose;
    internal Vector3 FeetWorld => new Vector3(Motor.X*scale,Motor.Y*scale,body.transform.position.z);
    private int probeFrame=-1;
    internal string Describe() => $"position=({Motor.X:F2},{Motor.Y:F2}) velocity=({Motor.SpeedX:F2},{Motor.SpeedY:F2}) state={Motor.State} ground={Motor.Grounded} stamina={Motor.Stamina:F1} dashes={Motor.Dashes}/{Motor.MaxDashes} probe={probeMode}:{probeFrame}";
    internal void StartProbe(string mode)
    {
        if(mode!="jump"&&mode!="dash"&&mode!="super"&&mode!="hyper"&&mode!="extended"&&mode!="transition")
            throw new ArgumentException("Expected status, jump, dash, super, hyper, extended or transition.");
        if(!Grounded||!MovementRuntime.Eligible(body)) throw new InvalidOperationException("Probe requires normal gameplay on the ground.");
        probeMode=mode; probeFrame=0;
        MovementRuntime.Monitor.Log("Probe start "+mode+" "+Describe());
    }
    internal bool Grounded => Motor.Grounded;
    internal bool Suspended;
    internal Vector2 LogicalVelocity => new Vector2(Motor.SpeedX,Motor.SpeedY)*scale;
    public override bool SupportJump => true;
    public override bool SupportDash => Motor.HasDash;
    public override bool SupportUseItem => Grounded&&!Motor.Dashing;
    public override bool SupportInteract => Grounded&&!Motor.Dashing;
    public override bool IsActiveState => true;
    protected override AgentStateBase NextState() => this;

    internal CelesteLocomotionState(AgentStateManager parent,BodyController body):base(parent,body)
    {
        rb=(Rigidbody2D)body.GetComponent(typeof(Rigidbody2D));
        collider=body.BodyCollider;
    }
    public override void OnEnter()
    {
        if(ownsPhysics) return;
        oldGravity=rb.gravityScale; oldDrag=rb.drag; oldType=rb.bodyType;
        oldCollision=rb.collisionDetectionMode; oldSize=collider.size; oldOffset=collider.offset;
        // Spatial calibration affects distances and collider dimensions together;
        // the original frame timing and pixel-space physics remain unchanged.
        if(!initialized) scale=Mathf.Max(.001f,oldSize.y*Mathf.Abs(collider.transform.lossyScale.y)/11f*
            (float)MovementRuntime.Config.SpatialScale);
        var bounds=collider.bounds;
        feetOffset=new Vector2(bounds.center.x-rb.position.x,bounds.min.y-rb.position.y);
        var feet=rb.position+feetOffset;
        var velocity=rb.velocity;
        RefreshAbilities();
        if(!Suspended) Motor.Reset(feet.x/scale,feet.y/scale,velocity.x/scale,velocity.y/scale,body.IsFaceRight?1:-1,!initialized);
        else Motor.Relocate(feet.x/scale,feet.y/scale);
        Suspended=false;
        initialized=true;
        world=new UnityPixelWorld(body,scale);
        Physics2D.SyncTransforms();
        waitingForClearance=!Motor.TryFit(world);
        if(waitingForClearance) MovementRuntime.Monitor.Log("Waiting for spawn clearance; Celeste controller retained.",DTMAPI.Abstractions.LogLevel.Warn);
        ownsPhysics=true; rb.gravityScale=0; rb.drag=0; rb.bodyType=RigidbodyType2D.Kinematic;
        rb.velocity=Vector2.zero; rb.collisionDetectionMode=CollisionDetectionMode2D.Discrete;
        accumulator=0; animation=new MovementAnimation(body); lastInputFrame=-1; ClearInput(); ApplyShape();
        crouchVisual=(CrouchVisual)body.GetComponent(typeof(CrouchVisual))??
            (CrouchVisual)body.gameObject.AddComponent(typeof(CrouchVisual));
        crouchVisual.Bind(body);
        climbVisual=(ClimbVisual)body.GetComponent(typeof(ClimbVisual))??
            (ClimbVisual)body.gameObject.AddComponent(typeof(ClimbVisual));
        climbVisual.Bind(body);
        statusVisual=(StatusVisual)body.GetComponent(typeof(StatusVisual))??
            (StatusVisual)body.gameObject.AddComponent(typeof(StatusVisual));
        statusVisual.Bind(body);
        MovementRuntime.Monitor.Log($"Movement attached; pixel solver at 60 Hz; scale={scale:F4}; native crouch pose={HasNativeCrouchPose}; dashes={Motor.Dashes}/{Motor.MaxDashes}");
    }
    private void RefreshAbilities()
    {
        var archive=DolocAPI.archiveHandle;
        // Read actual story flags, not the developer override in CouldDash /
        // DoubleJumpTimes. Polling also picks up unlocks without a reload.
        bool doubleJump=archive!=null&&archive.IsDoubleJumpUnlocked();
        bool dash=archive!=null&&archive.IsDashUnlocked();
        int capacity=PixelPlayer.DashCapacity(doubleJump,dash);
        if(Motor.MaxDashes!=capacity||!initialized)
            MovementRuntime.Monitor.Log($"Movement abilities: native doubleJump={doubleJump}; native dash={dash}; dash capacity={capacity}");
        Motor.SetDashCapacity(capacity);
    }
    public override void OnExit() => RestorePhysics();
    private void ClearInput()
    {
        input=default; jumpPending=dashPending=demoPending=dropPending=false;
        MotionPressed=false;
        sampleTime=-100; Motor.ClearInput();
        horizontal.Clear(); vertical.Clear();
    }
    internal void RestorePhysics()
    {
        ClearInput();
        if(!ownsPhysics) return;
        ownsPhysics=false;
        animation?.Restore();
        crouchVisual?.Restore();
        climbVisual?.Restore();
        statusVisual?.Restore();
        probeFrame=-1; probeMode="";
        if(collider!=null) { collider.size=oldSize; collider.offset=oldOffset; }
        if(rb!=null)
        {
            rb.bodyType=body.Pause?RigidbodyType2D.Static:
                body.InCutscene?RigidbodyType2D.Kinematic:oldType;
            rb.gravityScale=oldGravity; rb.drag=oldDrag; rb.collisionDetectionMode=oldCollision;
            if(!body.Pause)
                rb.velocity=body.InCutscene?Vector2.zero:new Vector2(Motor.SpeedX,Motor.SpeedY)*scale;
        }
        if(body!=null)
        {
            body.SortingOrder=0; body.SortingLayerName="Default";
            DolocAPI.Sound.PostSoundEvent(SoundEvents.STOP_CHARACTER_FOOTSTEP);
        }
    }
    internal void Sample()
    {
        if(lastInputFrame==Time.frameCount) return;
        lastInputFrame=Time.frameCount; sampleTime=Time.unscaledTime;
        var user=body.UserInput;
        var keys=MovementRuntime.Input;
        input.X=horizontal.Update(keys.IsKeybindDown("MoveLeft"),keys.IsKeybindDown("MoveRight"));
        input.Y=vertical.Update(keys.IsKeybindDown("MoveDown"),keys.IsKeybindDown("MoveUp"));
        bool nativeKeys=MovementRuntime.Config.UseGameJumpDash;
        input.JumpHeld=nativeKeys?user.NormalJumpInProgress:keys.IsKeybindDown("Jump");
        input.GrabHeld=MovementRuntime.Input.IsKeybindDown("Grab");
        bool jumpPressed=nativeKeys?user.NormalJump:keys.WasKeybindPressed("Jump");
        bool dashPressed=Motor.MaxDashes>0&&(nativeKeys?user.NormalDash:keys.WasKeybindPressed("Dash"));
        bool demoPressed=Motor.MaxDashes>0&&keys.WasKeybindPressed("CrouchDash");
        MotionPressed=jumpPressed||dashPressed||demoPressed;
        jumpPending|=jumpPressed;
        dashPending|=dashPressed;
        demoPending|=demoPressed;
    }
    public override void OnPlay() { }
    internal void FrameUpdate()
    {
        try { Tick(); }
        catch(Exception error) { MovementRuntime.Fault(error); }
    }
    private void Tick()
    {
        if(!MovementRuntime.Eligible(body)) { MovementRuntime.Release(); return; }
        RefreshAbilities();
        Sample();
        if(Time.deltaTime<=0) return;
        Physics2D.SyncTransforms();
        if(waitingForClearance)
        {
            if(!Motor.TryFit(world)) { ClearInput(); return; }
            waitingForClearance=false;
        }
        if(Time.unscaledTime-sampleTime>.1f) ClearInput();
        world.IgnorePlatforms=body.gameObject.layer==LayerMask.NameToLayer("PlatformExclude");
        Vector2 lift;
        float dt=Time.deltaTime;
        world.Transport(Motor,dt,out lift);
        var conveyor=Motor.Grounded?status.MoveModifier.Offset:Vector2.zero;
        Motor.Carry(conveyor.x*dt/scale,conveyor.y*dt/scale);
        accumulator+=dt;
        while(accumulator+0.000001f>=PixelPlayer.Frame)
        {
            accumulator-=PixelPlayer.Frame;
            input.JumpPressed=jumpPending; input.DashPressed=dashPending;
            input.CrouchDashPressed=demoPending; input.DropPressed=dropPending;
            jumpPending=dashPending=demoPending=dropPending=false;
            var frameInput=input;
            if(probeFrame>=0)
            {
                if(probeMode=="transition")
                    frameInput=new PlayerInput{X=probeFrame<120?1:-1};
                else if(probeMode=="jump")
                    frameInput=new PlayerInput{JumpPressed=probeFrame==0,JumpHeld=probeFrame<12};
                else if(probeMode=="dash")
                    frameInput=new PlayerInput{X=probeFrame<8?1:0,Y=probeFrame<8?1:0,DashPressed=probeFrame==0};
                else
                {
                    int jumpAt=probeMode=="extended"?8:3;
                    frameInput=new PlayerInput{X=probeFrame<12?1:0,
                        Y=probeMode=="super"||probeFrame>=jumpAt?0:-1,DashPressed=probeFrame==0,
                        JumpPressed=probeFrame==jumpAt,JumpHeld=probeFrame>=jumpAt&&probeFrame<jumpAt+12};
                }
                probeFrame++;
            }
            if(frameInput.Y<0 && frameInput.JumpPressed && Motor.TryDropThrough(world))
            {
                frameInput.JumpPressed=false; frameInput.JumpHeld=false;
                DolocAPI.Broadcast(OperationEventType.JUMP_DOWN_PLATFORM);
                if(MovementRuntime.Config.DiagnosticLogging) MovementRuntime.Monitor.Log("Motion action=platform-drop (down+jump)");
            }
            Motor.EnableWallJump=true;
            var offset=lift+conveyor;
            Motor.LiftX=offset.x/scale; Motor.LiftY=offset.y/scale;
            Motor.Tick(frameInput,world);
            if(probeFrame>=0&&probeFrame%10==0) MovementRuntime.Monitor.Log("Probe frame "+Describe());
            if(probeFrame>=(probeMode=="transition"?600:90))
            {
                MovementRuntime.Monitor.Log("Probe complete "+Describe());
                probeFrame=-1; probeMode="";
            }
            if(Motor.Jumped)
            {
                animation.OnJump(Motor.Action);
                body.CheckJumpDownReleased(true); body.HatRenderer.OnJump();
                DolocAPI.Sound.PostSoundEvent(SoundEvents.PLAY_CHARACTER_JUMP);
                DolocAPI.Broadcast(OperationEventType.JUMP);
            }
            if(Motor.DashStarted)
            {
                body.HatRenderer.OnDash(); shadowTimer=0;
                DolocAPI.Sound.PostSoundEvent(SoundEvents.PLAY_CHARACTER_DASH);
                DolocAPI.Broadcast(OperationEventType.SPRINT);
                MovementRuntime.Freeze();
                Motor.CompleteHostFreeze();
            }
            if(Motor.Landed)
            {
                body.AllowDash(true);
                DolocAPI.Sound.PostSoundEvent(SoundEvents.PLAY_CHARACTER_FOOTSTEP_DROP);
            }
            if(MovementRuntime.Config.DiagnosticLogging&&Motor.Action!="")
                MovementRuntime.Monitor.Log($"Motion action={Motor.Action} velocity=({Motor.SpeedX:F2},{Motor.SpeedY:F2}) stamina={Motor.Stamina:F1} dash={Motor.HasDash}");
            if(Motor.DashStarted) { accumulator=0; break; }
        }
        body.IsFaceRight=Motor.Facing>0;
        ApplyShape();
        rb.position=new Vector2(Motor.X,Motor.Y)*scale-feetOffset;
        rb.velocity=Vector2.zero;
        if(Motor.Dashing&&!Motor.Frozen)
        {
            shadowTimer-=dt;
            if(shadowTimer<=0) { body.GhostShadow(); shadowTimer=.05f; }
        }
        animation.Update(Motor);
        body.SortingOrder=Grounded?0:1;
        logTimer-=dt;
        if(MovementRuntime.Config.DiagnosticLogging&&logTimer<=0)
        {
            logTimer=1;
            MovementRuntime.Monitor.Log($"Motion pixels=({Motor.X:F2},{Motor.Y:F2}) velocity=({Motor.SpeedX:F2},{Motor.SpeedY:F2}) state={Motor.State} ground={Grounded} duck={Motor.Ducking} stamina={Motor.Stamina:F1} dash={Motor.HasDash}");
        }
    }
    private void ApplyShape()
    {
        var factor=collider.transform.lossyScale;
        collider.size=new Vector2(8*scale/Mathf.Abs(factor.x),Motor.Height*scale/Mathf.Abs(factor.y));
        float oldBottom=oldOffset.y-oldSize.y*.5f;
        collider.offset=new Vector2(oldOffset.x,oldBottom+collider.size.y*.5f);
    }
}
