using System;

namespace Doloc.CelesteMovement.Core
{
    // Celeste status timing with native hair at full charge and red at 1/2.
    public sealed class StatusFeedback
    {
        public float R { get; private set; } = 172f/255;
        public float G { get; private set; } = 50f/255;
        public float B { get; private set; } = 50f/255;
        public bool FlashRed { get; private set; }
        public bool FlashWhite { get; private set; }
        // The Doloc outfit owns its normal hair color; only status cues override it.
        public bool UseNativeHair { get; private set; } = true;
        private int lastDashes=-1;
        private float hairFlash;
        public void Update(float dt,int dashes,float checkStamina,float sceneTime,int maxDashes=1)
        {
            dt=Math.Max(0,dt);
            FlashRed=checkStamina<20 && ((int)Math.Floor(sceneTime/.05f)&1)!=0;
            FlashWhite=false;
            if(lastDashes>dashes) hairFlash=0;
            if(maxDashes==0)
            {
                UseNativeHair=true; hairFlash=0; lastDashes=0;
                R=172f/255; G=B=50f/255;
                return;
            }
            if(dashes==0)
            {
                float t=Math.Min(1,6*dt);
                R+=(68f/255-R)*t; G+=(183f/255-G)*t; B+=(1-B)*t;
            }
            else
            {
                if(lastDashes>=0&&lastDashes<dashes) { hairFlash=.12f; FlashWhite=true; }
                else if(hairFlash>0) { FlashWhite=true; hairFlash-=dt; }
                if(FlashWhite) R=G=B=1;
                else { R=172f/255; G=B=50f/255; }
            }
            UseNativeHair=dashes>=maxDashes&&!FlashWhite;
            lastDashes=dashes;
        }
    }
}
