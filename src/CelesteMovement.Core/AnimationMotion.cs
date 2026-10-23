using System;

namespace Doloc.CelesteMovement.Core
{
    // Presentation only: never writes to the solver or its input.
    public sealed class AnimationMotion
    {
        public bool CrouchPose { get; private set; }
        public float CrouchAmount => crouch;
        public int CrouchFrame { get; private set; } = 3;
        public bool Climbing { get; private set; }
        public float Phase { get; private set; }
        public float Effort { get; private set; }
        public float Breath { get; private set; }
        private float crouch, idle, slide;
        private bool wasClimbing;
        public void Update(float dt,bool duck,bool grounded,bool dash,bool climb,float vx,float vy)
        {
            dt=Math.Max(0,Math.Min(.1f,dt));
            idle+=dt;
            Breath=(float)Math.Sin(idle*Math.PI*2/2.4f);
            if(climb)
            {
                if(!wasClimbing) { Phase=0; Effort=0; }
                Phase+=vy*dt/32f;
                Phase-=(float)Math.Floor(Phase);
                Effort=Approach(Effort,Math.Abs(vy)>.5f?1:0,dt*8);
            }
            else { Phase=0; Effort=0; }
            Climbing=wasClimbing=climb;
            if(climb||(!duck&&(!grounded||dash))) crouch=0;
            else crouch=Approach(crouch,duck?1:0,dt/.1f);
            CrouchPose=duck||crouch>0;
            if(crouch<.999f)
                CrouchFrame=Math.Min(3,Math.Max(0,(int)Math.Ceiling((1-crouch)*3)));
            else if(Math.Abs(vx)>8||dash)
            {
                slide+=dt*Math.Max(5,Math.Min(12,Math.Abs(vx)/20));
                CrouchFrame=((int)slide)%2;
            }
            else CrouchFrame=Breath>.75f?1:0;
        }
        private static float Approach(float value,float target,float amount) =>
            value<target?Math.Min(target,value+amount):Math.Max(target,value-amount);
        // Bone weights in base-sprite coordinates. Deformation is measured in art pixels.
        public void Offset(float x,float y,bool headLayer,out float dx,out float dy)
        {
            if(CrouchPose)
            {
                // Preserve head proportions. Fold the lower body and lean into
                // the crouch; feet stay planted instead of scaling the sprite.
                float upper=headLayer?1:Smooth(.15f,.55f,y);
                dx=upper*crouch*1.2f;
                dy=upper*crouch*(-1.8f+Breath*.12f);
                return;
            }
            float wave=(float)Math.Sin(Phase*Math.PI*2)*Effort;
            float other=(float)Math.Cos(Phase*Math.PI*2)*Effort;
            float bob=(float)Math.Cos(Phase*Math.PI*4)*.28f*Effort+Breath*.12f*(1-Effort);
            dx=0; dy=bob;
            if(headLayer) return;
            float foot=1-Smooth(.24f,.44f,y);
            float left=1-Smooth(.43f,.62f,x);
            float arms=Smooth(.28f,.47f,y)*(1-Smooth(.56f,.72f,y));
            float edges=Smooth(.58f,.82f,x)+(1-Smooth(.20f,.38f,x));
            dx=foot*(left-.5f)*other*.65f+arms*edges*other*.35f;
            dy+=foot*(left*2-1)*wave*1.6f+arms*edges*(x>.5f?-wave:wave)*1.8f;
        }
        private static float Smooth(float a,float b,float value)
        {
            float t=Math.Max(0,Math.Min(1,(value-a)/(b-a)));
            return t*t*(3-2*t);
        }
    }
}
