using System;

namespace Doloc.CelesteMovement.Core
{
    // Feet-centred coordinates; positive Y points up. All distances are Celeste pixels.
    // The host supplies geometry only: no Unity solver velocities enter this simulation.
    public interface IPixelWorld
    {
        bool Solid(float left, float bottom, float width, float height);
        bool OneWay(float left, float bottom, float width, float height, float previousBottom);
        bool OverlapsOneWay(float left,float bottom,float width,float height);
        float? DashPlatformTop(float left,float bottom,float width,float height);
    }

    public struct PlayerInput
    {
        public int X, Y;
        public bool JumpPressed, JumpHeld, DashPressed, GrabHeld, CrouchDashPressed, DropPressed;
    }

    public enum PlayerMotion { Normal, Climb, Dash }

    public sealed class PixelPlayer
    {
        // FNA's 60 Hz GameTime is a 166667-tick TimeSpan, converted to seconds.
        public const float Frame = .0166667f;
        public float X { get; private set; }
        public float Y { get; private set; }
        public float SpeedX { get; private set; }
        public float SpeedY { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Ducking { get; private set; }
        public bool Grounded { get; private set; }
        public int MaxDashes { get; private set; } = 1;
        public int Dashes { get; private set; } = 1;
        public bool HasDash => Dashes>0;
        public float Stamina { get; private set; } = 110;
        public float CheckStamina => Stamina+(wallBoost>0?27.5f:0);
        public PlayerMotion State { get; private set; }
        public bool Dashing => State == PlayerMotion.Dash;
        public bool Frozen => freeze > 0;
        public bool DashAttacking => attack > 0;
        public bool EnableWallJump = true;
        public bool Jumped { get; private set; }
        public bool DashStarted { get; private set; }
        public bool Landed { get; private set; }
        public string Action { get; private set; } = "";
        public float Height => Ducking ? 6 : 11;
        public float LiftX, LiftY;
        private float rx, ry, grace, jumpBuffer, dashBuffer, variable, variableSpeed;
        private float cooldown, refill, dashTime, freeze, attack, dashX, dashY, beforeDashX;
        private float wallSlide = 1.2f, wallBoost, forceTime, climbWait, retainTime, retainedX;
        private float fallLimit = 160, dropTime, dropFromY, lastLiftX, lastLiftY, liftGrace;
        private int forceX, wallBoostDir, hopWait, previousMove;
        private bool dashOnGround, launchPending, demoPending, autoJump;
        private PlayerInput input;
        private IPixelWorld world = null!;

        public void Reset(float x, float y, float vx = 0, float vy = 0, int facing = 1, bool refillDash = true)
        {
            X=x; Y=y; SpeedX=vx; SpeedY=vy; Facing=facing<0?-1:1;
            State=PlayerMotion.Normal; Ducking=false; Grounded=false;
            if (refillDash) { Dashes=MaxDashes; Stamina=110; }
            rx=ry=grace=jumpBuffer=dashBuffer=variable=cooldown=refill=dashTime=freeze=attack=0;
            wallBoost=forceTime=climbWait=retainTime=dropTime=liftGrace=0;
            lastLiftX=lastLiftY=0; wallSlide=1.2f; fallLimit=160; hopWait=previousMove=0;
            launchPending=demoPending=autoJump=false; ClearInput();
        }

        // The later native dash unlock is the two-charge milestone, including
        // old saves where only that flag is present. Never write native progress.
        public static int DashCapacity(bool doubleJumpUnlocked,bool dashUnlocked) =>
            dashUnlocked?2:doubleJumpUnlocked?1:0;
        public void SetDashCapacity(int capacity)
        {
            capacity=Math.Max(0,Math.Min(2,capacity));
            if(capacity==MaxDashes) return;
            Dashes=Math.Min(capacity,Dashes+Math.Max(0,capacity-MaxDashes));
            MaxDashes=capacity;
            dashBuffer=0; demoPending=false;
        }
        public void ClearInput() { input=default; jumpBuffer=dashBuffer=0; demoPending=false; }
        public void Relocate(float x, float y) { X=x; Y=y; rx=ry=0; }
        public void Carry(float x,float y) { X+=x; Y+=y; }
        public void SetVelocity(float x, float y) { SpeedX=x; SpeedY=y; }
        public void DropThrough() { dropTime=.25f; dropFromY=Y; grace=0; Grounded=false; }
        private bool PlatformsAt(float y) => dropTime<=0 || y<dropFromY;
        // Doloc keyboard adapter extension: consume down+jump only on a one-way
        // support, never during a dash (where the same chord can be a hyper).
        public bool TryDropThrough(IPixelWorld geometry)
        {
            if(State!=PlayerMotion.Normal || !Grounded || SpeedY>0 ||
               geometry.Solid(X-4,Y-1,8,Height) ||
               !geometry.OneWay(X-4,Y-1,8,Height,Y)) return false;
            DropThrough();
            jumpBuffer=variable=0; autoJump=false; SpeedY=Math.Min(0,SpeedY);
            return true;
        }
        public void CompleteHostFreeze() { freeze=0; }
        public bool Overlapping(IPixelWorld geometry) => geometry.Solid(X-4,Y,8,Height);
        // Bounded spawn/handoff repair only; never used to ignore a movement collision.
        public bool TryFit(IPixelWorld geometry)
        {
            if(!Overlapping(geometry)) return true;
            if(!geometry.Solid(X-4,Y,8,6)) { Ducking=true; return true; }
            for(int distance=1;distance<=4;distance++)
            {
                if(!geometry.Solid(X-4,Y+distance,8,Height)) { Relocate(X,Y+distance); return true; }
                for(int sign=-1;sign<=1;sign+=2)
                    if(!geometry.Solid(X-4+distance*sign,Y,8,Height)) { Relocate(X+distance*sign,Y); return true; }
            }
            return false;
        }

        public void Tick(PlayerInput next, IPixelWorld geometry)
        {
            world=geometry; input=next; input.X=Math.Sign(input.X); input.Y=Math.Sign(input.Y);
            Jumped=DashStarted=Landed=false; Action="";
            if(input.JumpPressed) jumpBuffer=.08f;
            if(MaxDashes>0 && (input.DashPressed || input.CrouchDashPressed)) { dashBuffer=.08f; demoPending=input.CrouchDashPressed; }
            // Input buffers survive the three-frame dash freeze, as in the original engine.
            if(freeze>0) { freeze=Down(freeze); return; }
            dropTime=Down(dropTime);
            if(input.DropPressed) DropThrough();
            bool wasGrounded=Grounded;
            Grounded=SpeedY<=0 && GroundAt(X,Y);
            Landed=Grounded&&!wasGrounded;
            if(Grounded)
            {
                grace=.1f; wallSlide=1.2f; Stamina=110; autoJump=false;
            }
            else grace=Down(grace);
            if(refill>0) refill=Down(refill);
            else if(Grounded) Dashes=MaxDashes;
            cooldown=Down(cooldown); attack=Down(attack); variable=Down(variable);
            if(LiftX!=0 || LiftY!=0) { lastLiftX=LiftX; lastLiftY=LiftY; liftGrace=.1f; }
            else if((liftGrace=Down(liftGrace))<=0) lastLiftX=lastLiftY=0;
            if(State==PlayerMotion.Normal && wasGrounded && !Grounded && SpeedY<=0 && BoostY>0)
                SpeedY=BoostY;
            if(wallBoost>0)
            {
                wallBoost=Down(wallBoost);
                // Player.Update checks wall-boost before refreshing moveX for this frame.
                if(previousMove==wallBoostDir) { SpeedX=130*previousMove; Stamina+=27.5f; wallBoost=0; Action="wall-boost"; }
            }
            int move=forceTime>0?forceX:input.X;
            previousMove=move;
            forceTime=Down(forceTime);
            if(State!=PlayerMotion.Climb && move!=0) Facing=move;
            if(retainTime>0)
            {
                if(Math.Sign(SpeedX)==-Math.Sign(retainedX)) retainTime=0;
                else if(!SolidAt(X+Math.Sign(retainedX),Y))
                { SpeedX=retainedX; retainTime=0; }
                else retainTime=Down(retainTime);
            }
            if(hopWait!=0)
            {
                if(SpeedY<0||Math.Sign(SpeedX)==-hopWait) hopWait=0;
                else if(!SolidAt(X+hopWait,Y)) { SpeedX=hopWait*100; hopWait=0; }
            }
            if(launchPending) LaunchDash();
            else if(State==PlayerMotion.Climb) Climb();
            else if(State==PlayerMotion.Dash) Dash();
            else Normal(move);
            if(!Frozen)
            {
                // Original post-state assists run before velocity movement.
                if(!Grounded && SpeedY>=0 && (State!=PlayerMotion.Climb||input.Y>0) &&
                    PlatformsAt(Y) && world.OverlapsOneWay(X-4,Y,8,Height))
                    MoveV(40*Frame);
                if(!Grounded && DashAttacking && dashY==0 && GroundAt(X,Y-2))
                    for(int i=0;i<3&&!GroundAt(X,Y);i++) Y--;
                if(!Grounded && SpeedY<0 && grace<=0 && CanStand()) Ducking=false;
                Move();
            }
            Finish();
        }

        private void Normal(int move)
        {
            if(input.GrabHeld && Stamina+(wallBoost>0?27.5f:0)>=20 && !Ducking &&
               SpeedY<=0 && Math.Sign(SpeedX)!=-Facing)
            {
                if(SolidAt(X+Facing*2,Y)) { BeginClimb(); return; }
                if(input.Y>=0)
                    for(int up=1;up<=2;up++)
                        if(!SolidAt(X,Y+up)&&SolidAt(X+Facing*2,Y+up))
                        { Y+=up; BeginClimb(); return; }
            }
            if(CanDash()) { BeginDash(); return; }
            if(Grounded)
            {
                if(input.Y<0) Ducking=true;
                else if(CanStand()) Ducking=false;
                else if(SpeedX==0)
                    for(int i=4;i>0;i--)
                    {
                        int dir=CanStand(X+i,Y)?1:CanStand(X-i,Y)?-1:0;
                        if(dir!=0) { MoveH(dir*50*Frame); break; }
                    }
            }
            if(Ducking&&Grounded) SpeedX=Approach(SpeedX,0,500*Frame);
            else
            {
                float accel=Math.Abs(SpeedX)>90&&Math.Sign(SpeedX)==move?400:1000;
                SpeedX=Approach(SpeedX,move*90,accel*(Grounded?1:.65f)*Frame);
            }
            fallLimit=Approach(fallLimit,input.Y<0&&SpeedY<=-160?240:160,300*Frame);
            if(!Grounded)
            {
                float limit=fallLimit;
                if(SpeedY<=0&&wallSlide>0&&input.Y>=0&&CanStand()&&
                    (move==Facing||(move==0&&input.GrabHeld))&&SolidAt(X+Facing,Y))
                { Ducking=false; limit=160+(20-160)*(wallSlide/1.2f); wallSlide=Down(wallSlide); }
                SpeedY=Approach(SpeedY,-limit,900*(Math.Abs(SpeedY)<40&&(input.JumpHeld||autoJump)?.5f:1)*Frame);
            }
            else if(SpeedY<0) SpeedY=0;
            if(variable>0)
            {
                if(input.JumpHeld||autoJump) SpeedY=Math.Max(SpeedY,variableSpeed);
                else variable=0;
            }
            if(jumpBuffer>0)
            {
                if(grace>0) Jump("jump",SpeedX+40*move,105,.2f,false);
                else TryWallJump(false);
            }
        }

        private bool CanDash() => dashBuffer>0&&HasDash&&cooldown<=0;
        private void BeginDash()
        {
            wallBoost=retainTime=0; hopWait=0;
            beforeDashX=SpeedX+BoostX; dashOnGround=Grounded;
            State=PlayerMotion.Dash; freeze=.05f; launchPending=true;
            cooldown=.2f; refill=.1f; attack=.3f; wallSlide=1.2f;
            SpeedX=SpeedY=dashX=dashY=0; Dashes--;
            if(!Grounded&&Ducking&&CanStand()) Ducking=false;
            else if(!Ducking&&(demoPending||input.Y<0)) Ducking=true;
            dashBuffer=0; DashStarted=true; Action="dash-freeze";
        }
        private void LaunchDash()
        {
            launchPending=false; demoPending=false;
            int ax=input.X, ay=input.Y;
            if(ax==0&&ay==0) ax=Facing;
            float norm=ax!=0&&ay!=0?.70710678118f:1;
            dashX=ax*norm; dashY=ay*norm; SpeedX=dashX*240; SpeedY=dashY*240;
            if(Math.Sign(beforeDashX)==Math.Sign(SpeedX)&&Math.Abs(beforeDashX)>Math.Abs(SpeedX)) SpeedX=beforeDashX;
            if(ax!=0) Facing=ax;
            dashTime=.15f;
            if(Grounded&&dashX!=0&&dashY<0) Slide();
            Action="dash";
        }
        private void Dash()
        {
            if(Math.Abs(dashY)<.1f&&PlatformsAt(Y))
            {
                float? top=world.DashPlatformTop(X-4,Y,8,Height);
                if(top.HasValue && top.Value>Y && top.Value-Y<=6 && !SolidAt(X,top.Value))
                    Y=top.Value;
            }
            if(jumpBuffer>0&&CanStand())
            {
                if(Math.Abs(dashY)<.1f&&grace>0)
                {
                    bool hyper=Ducking;
                    Jump(hyper?"hyper":"super",260*Facing,105,.2f,true);
                    if(hyper) { SpeedX*=1.25f; SpeedY*=.5f; variableSpeed=SpeedY; }
                    return;
                }
                if(TryWallJump(true)) return;
            }
            // Monocle's coroutine resumes on the update AFTER its wait expires.
            if(dashTime>0) dashTime=Down(dashTime);
            else
            {
                if(dashY>=0) { SpeedX=dashX*160; SpeedY=dashY*160; }
                if(SpeedY>0) SpeedY*=.75f;
                State=PlayerMotion.Normal; fallLimit=160; autoJump=true;
            }
        }
        private void Slide()
        {
            dashX=Math.Sign(dashX); dashY=0; SpeedX*=1.2f; SpeedY=0;
            Ducking=true; Action="dash-slide";
        }
        private bool TryWallJump(bool inDash)
        {
            if(!EnableWallJump||!CanStand()) return false;
            bool bounce=DashAttacking&&Math.Abs(dashX)<=.2f&&dashY>=.75f;
            int distance=bounce?5:3;
            int wall=SolidAt(X+distance,Y)?1:SolidAt(X-distance,Y)?-1:0;
            if(wall==0) return false;
            bool climbJump=Facing==wall&&input.GrabHeld&&Stamina>0;
            if(bounce && (inDash||!climbJump))
                Jump("wallbounce",-wall*170,160,.25f,true);
            else if(climbJump)
                ClimbJump();
            else
            {
                Jump("wall-jump",-wall*130,105,.2f,true);
                if(input.X!=0) { forceX=-wall; forceTime=.16f; }
            }
            return true;
        }
        private void Jump(string action,float vx,float vy,float duration,bool unduck)
        {
            State=PlayerMotion.Normal; fallLimit=160; SpeedX=vx+BoostX; SpeedY=vy+BoostY;
            if(unduck) Ducking=false;
            variableSpeed=SpeedY; variable=duration; jumpBuffer=grace=attack=wallBoost=0;
            wallSlide=1.2f; autoJump=false; Jumped=true; Action=action; Grounded=false;
        }
        private void BeginClimb()
        {
            State=PlayerMotion.Climb; Ducking=false; SpeedX=0; SpeedY*=.2f;
            wallSlide=1.2f; climbWait=.1f; wallBoost=0; autoJump=false; retainTime=0;
            for(int i=0;i<2&&!SolidAt(X+Facing,Y);i++) X+=Facing;
            Action="grab";
        }
        private void ClimbJump()
        {
            if(!Grounded) Stamina-=27.5f;
            Jump("climb-jump",SpeedX+40*input.X,105,.2f,true);
            if(input.X==0) { wallBoostDir=-Facing; wallBoost=.2f; }
        }
        private void Climb()
        {
            climbWait=Down(climbWait);
            if(jumpBuffer>0&&CanStand())
            {
                if(input.X==-Facing)
                {
                    Jump("wall-jump",-Facing*130,105,.2f,true);
                    forceX=-Facing; forceTime=.16f;
                }
                else ClimbJump();
                return;
            }
            if(CanDash()) { BeginDash(); return; }
            if(!input.GrabHeld) { ReleaseClimb(); return; }
            if(!SolidAt(X+Facing,Y))
            {
                if(SpeedY>0) Hop();
                else ReleaseClimb();
                return;
            }
            float target=0; int cost=0;
            if(climbWait<=0)
            {
                if(input.Y>0)
                {
                    target=45; cost=1;
                    if(SolidAt(X,Y+1)) { target=0; cost=0; SpeedY=Math.Min(0,SpeedY); }
                    else if(Slip()) { Hop(); return; }
                }
                else if(input.Y<0) { target=Grounded?0:-80; cost=Grounded?0:-1; }
                if(cost==0&&Slip()) target=-30;
            }
            else if(Slip()) target=-30;
            SpeedY=Approach(SpeedY,target,900*Frame);
            if(input.Y>=0&&SpeedY<0&&!SolidAt(X+Facing,Y-1)) SpeedY=0;
            if(climbWait<=0) Stamina-= (cost>0?45.454544f:cost==0?10:0)*Frame;
            if(Stamina<=0) ReleaseClimb();
        }
        private bool Slip() => !world.Solid(X+(Facing>0?4:-5),Y+7,1,1)&&
                               !world.Solid(X+(Facing>0?4:-5),Y+11,1,1);
        private void Hop()
        {
            State=PlayerMotion.Normal; fallLimit=160; SpeedY=Math.Max(SpeedY,120);
            if(SolidAt(X+Facing,Y)) { hopWait=Facing; SpeedX=0; }
            else SpeedX=Facing*100;
            forceX=0; forceTime=.2f; Action="climb-hop";
        }
        private void ReleaseClimb() { State=PlayerMotion.Normal; fallLimit=160; SpeedX+=BoostX; SpeedY+=BoostY; retainTime=0; }
        private float BoostX => Math.Max(-250,Math.Min(250,lastLiftX));
        private float BoostY => Math.Max(0,Math.Min(130,lastLiftY));

        private bool SolidAt(float x,float y) => world.Solid(x-4,y,8,Height);
        private bool CanStand() => CanStand(X,Y);
        private bool CanStand(float x,float y) => !world.Solid(x-4,y,8,11);
        private bool GroundAt(float x,float y) => SolidAt(x,y-1)||
            (PlatformsAt(y)&&world.OneWay(x-4,y-1,8,Height,y));
        private void Move()
        {
            MoveH(SpeedX*Frame);
            MoveV(SpeedY*Frame);
        }
        private void MoveH(float amount)
        {
            rx+=amount; int pixels=(int)Math.Round(rx,MidpointRounding.ToEven); rx-=pixels;
            int dir=Math.Sign(pixels);
            for(int i=0;i<Math.Abs(pixels);i++)
            {
                if(!SolidAt(X+dir,Y)) { X+=dir; continue; }
                if(Dashing)
                {
                    if(Grounded&&!world.Solid(X+dir-4,Y,8,6)) { Ducking=true; return; }
                    if(SpeedY==0&&SpeedX!=0)
                    {
                        for(int offset=1;offset<=4;offset++)
                            for(int sign=-1;sign<=1;sign+=2)
                                if(!SolidAt(X+dir,Y+offset*sign)&&SolidAt(X+dir,Y+(offset-1)*sign))
                                { Y+=offset*sign; X+=dir; return; }
                    }
                }
                if(retainTime<=0) { retainedX=SpeedX; retainTime=.06f; }
                SpeedX=0; attack=0; rx=0; return;
            }
        }
        private void MoveV(float amount)
        {
            ry+=amount; int pixels=(int)Math.Round(ry,MidpointRounding.ToEven); ry-=pixels;
            int dir=Math.Sign(pixels);
            for(int i=0;i<Math.Abs(pixels);i++)
            {
                bool blocked=SolidAt(X,Y+dir)||(dir<0&&PlatformsAt(Y)&&world.OneWay(X-4,Y-1,8,Height,Y));
                if(!blocked) { Y+=dir; continue; }
                if(dir>0)
                {
                    int max=DashAttacking&&Math.Abs(SpeedX)<.01f?5:4;
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        if(sign<0&&SpeedX>.01f||sign>0&&SpeedX<-.01f) continue;
                        for(int j=1;j<=max;j++)
                            if(!SolidAt(X+j*sign,Y+1))
                            { X+=j*sign; Y++; return; }
                    }
                    if(variable<.15f) variable=0;
                }
                else
                {
                    if(Dashing&&!dashOnGround)
                        for(int sign=-1;sign<=1;sign+=2)
                        {
                            if(sign<0&&SpeedX>.01f||sign>0&&SpeedX<-.01f) continue;
                            for(int j=1;j<=4;j++)
                                if(!GroundAt(X+j*sign,Y)&&!SolidAt(X+j*sign,Y))
                                { X+=j*sign; Y--; return; }
                        }
                    if(dashX!=0&&dashY<0&&SpeedY<0) Slide();
                    Landed=!Grounded;
                }
                SpeedY=0; attack=0; ry=0; return;
            }
        }
        private void Finish() { jumpBuffer=Down(jumpBuffer); dashBuffer=Down(dashBuffer); }
        private static float Down(float t) => t<=Frame+.000001f?0:t-Frame;
        private static float Approach(float v,float target,float amount) =>
            v<target?Math.Min(v+amount,target):Math.Max(v-amount,target);
    }
}
