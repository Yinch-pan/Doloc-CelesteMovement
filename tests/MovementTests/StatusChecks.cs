using Doloc.CelesteMovement.Core;

internal static class StatusChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string label) { if(!ok) throw new Exception(label); count++; Console.WriteLine("PASS "+label); }
        var f=new StatusFeedback();
        Check(f.UseNativeHair,"New presentation preserves the outfit hair before its first update");
        f.Update(1f/60,1,110,0);
        Check(f.UseNativeHair&&!f.FlashWhite,"Charged hair uses the original outfit without a false refill flash");
        float oldR=f.R;
        f.Update(1f/60,0,110,0);
        Check(!f.UseNativeHair&&f.R<oldR&&f.R>68f/255&&!f.FlashWhite,"Spending dash enables the blue cue without flashing white");
        for(int i=0;i<150;i++) f.Update(1f/60,0,110,i/60f);
        Check(Math.Abs(f.B-1)<.00001f&&Math.Abs(f.G-183f/255)<.00001f,"Empty hair converges to Celeste blue");
        f.Update(1f/60,1,110,3);
        Check(!f.UseNativeHair&&f.FlashWhite&&f.R==1&&f.G==1&&f.B==1,"Refilling dash overrides native hair briefly with white");
        for(int i=0;i<7;i++) f.Update(1f/60,1,110,3);
        Check(f.FlashWhite,"Refill flash lasts through 0.12 seconds");
        for(int i=0;i<3;i++) f.Update(1f/60,1,110,3);
        Check(!f.FlashWhite&&f.UseNativeHair,"Refill flash restores the outfit hair");
        f.Update(1f/60,1,20,.075f);
        Check(!f.FlashRed,"Exactly 20 stamina is not tired");
        f.Update(1f/60,1,19.99f,.075f);
        Check(f.FlashRed&&f.UseNativeHair,"Low stamina warning does not force a permanent hair tint");
        f.Update(1f/60,1,0,.125f);
        Check(!f.FlashRed,"Exhaustion alternates back at 0.05 second intervals");
        f.Update(1f/60,1,110,.175f);
        Check(!f.FlashRed,"Stamina refill clears warning immediately");
        var pose=new AnimationMotion();
        pose.Update(.1f,true,true,false,false,0,0);
        pose.Offset(.5f,0,false,out float footX,out float footY);
        pose.Offset(.5f,.9f,false,out float headX,out float headY);
        pose.Offset(.5f,.4f,true,out float hatX,out float hatY);
        Check(footX==0&&footY==0&&headY< -1,"New crouch keeps feet planted while lowering upper body");
        Check(headX==hatX&&headY==hatY,"Crouch keeps head and hat aligned without squashing");
        Console.WriteLine($"{count} status and crouch checks passed.");
    }
}
