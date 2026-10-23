using System.Collections.Generic;
using Doloc.CelesteMovement.Core;
using DolocTown;
using UnityEngine;

namespace Yinch.CelesteMovement;

internal sealed class MovementAnimation
{
    private readonly BodyController body;
    private readonly Animator animator;
    private readonly List<Clock> clocks=new List<Clock>();
    internal readonly AnimationMotion Motion=new AnimationMotion();
    internal readonly StatusFeedback Feedback=new StatusFeedback();
    private AgentHatRenderer hat;
    private string current="";
    private bool wallJumpPose, holding;
    private int sampledFrame=-1;
    private float preview=-1;
    private string previewStep="";
    private readonly float landingDuration=.4166667f;
    private readonly string crouchClip;
    internal bool HasCrouchPose { get; }
    private sealed class Clock { internal Animator Animator; internal float Speed; }

    internal MovementAnimation(BodyController body)
    {
        this.body=body;
        animator=(Animator)body.GetComponent(typeof(Animator));
        // The landing silhouette resembles the aerial pose. Use the game's
        // separate folded-leg sitting artwork, with a crouch-specific rig.
        crouchClip=animator!=null&&animator.HasState(0,Animator.StringToHash("sit"))?"sit":"idle";
        HasCrouchPose=animator!=null&&animator.HasState(0,Animator.StringToHash(crouchClip));
        if(animator?.runtimeAnimatorController!=null)
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
                if(clip.name==crouchClip) landingDuration=Mathf.Max(.001f,clip.length);
        if(MovementRuntime.TakeAnimationPreview()) preview=0;
    }

    internal void OnJump(string action) =>
        wallJumpPose=action=="wall-jump"||action=="wallbounce"||action=="climb-jump";

    internal void Update(PixelPlayer motor)
    {
        bool duck=motor.Ducking, grounded=motor.Grounded, dash=motor.Dashing;
        bool climb=motor.State==PlayerMotion.Climb;
        float vx=motor.SpeedX,vy=motor.SpeedY;
        int dashes=motor.Dashes, capacity=motor.MaxDashes;
        float stamina=motor.CheckStamina;
        if(preview>=0)
        {
            preview+=Time.deltaTime;
            if(preview>=14) { preview=-1; MovementRuntime.Monitor.Log("Animation preview complete."); }
            else
            {
                capacity=1;
                // Rendering only. Position, collisions and input remain live.
                duck=preview<2.6f; grounded=true; dash=false;
                climb=preview>=3.2f&&preview<8; vx=preview>1.6f&&duck?180:0;
                vy=preview<4.8f?45:preview<6.4f?-45:0;
                if(preview>=8)
                {
                    dashes=preview<10?0:1;
                    stamina=preview>=11&&preview<13?10:110;
                }
                string step=duck?(vx>0?"crouch-slide":"crouch-breathe"):climb?
                    (vy>0?"climb-up":vy<0?"climb-down":"climb-hold"):preview<8?"stand-up":
                    preview<10?"hair-empty":preview<11?"hair-refill":preview<13?"stamina-red-flash":"recovered";
                if(step!=previewStep) { previewStep=step; MovementRuntime.Monitor.Log("Animation preview "+step); }
            }
        }
        Feedback.Update(Time.deltaTime,dashes,stamina,Time.timeSinceLevelLoad,capacity);
        Motion.Update(Time.deltaTime,duck,grounded,dash,climb,vx,vy);
        if(grounded||vy<=0||dash||climb) wallJumpPose=false;
        bool crouch=Motion.CrouchPose&&HasCrouchPose;
        string next=crouch?crouchClip:dash?"dash":climb?"climb":
            vy>.01f?(wallJumpPose?"climb_jump":"jump"):!grounded?"drop":
            System.Math.Abs(vx)>1?"run":"idle";
        bool changed=next!=current||hat!=body.HatRenderer;
        if(changed)
        {
            RestoreClocks();
            if(current=="run") DolocAPI.Sound.PostSoundEvent(SoundEvents.STOP_CHARACTER_FOOTSTEP);
            body.PlayAnimation(next);
            current=next; hat=body.HatRenderer; sampledFrame=-1;
        }
        if(crouch)
        {
            if(!holding)
            {
                Hold(animator);
                if(hat!=null)
                    foreach(var component in hat.GetComponentsInChildren(typeof(Animator),true))
                        Hold((Animator)component);
                holding=true;
            }
            // Sit is distinct from the landing/jump poses. Keep its low frames.
            if(sampledFrame!=Motion.CrouchFrame||changed)
            {
                float time=(System.Math.Min(2,Motion.CrouchFrame)/12f+.001f)/landingDuration;
                body.PlayAnimation(crouchClip,time);
                foreach(var clock in clocks)
                    if(clock.Animator!=null)
                    {
                        clock.Animator.speed=0;
                        if(clock.Animator.isActiveAndEnabled) clock.Animator.Update(0);
                    }
                sampledFrame=Motion.CrouchFrame;
            }
        }
        if(holding) foreach(var clock in clocks)
            if(clock.Animator!=null) clock.Animator.speed=0;
    }
    private void Hold(Animator value)
    {
        if(value==null||clocks.Exists(c=>c.Animator==value)) return;
        clocks.Add(new Clock{Animator=value,Speed=value.speed});
        value.speed=0;
    }
    private void RestoreClocks()
    {
        foreach(var clock in clocks)
            if(clock.Animator!=null) clock.Animator.speed=clock.Speed;
        clocks.Clear(); holding=false;
    }
    internal void Restore()
    {
        RestoreClocks(); current=""; hat=null; wallJumpPose=false;
    }
}
