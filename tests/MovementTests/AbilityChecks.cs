using Doloc.CelesteMovement.Core;

internal static class AbilityChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string label)
        { if(!ok) throw new Exception(label); count++; Console.WriteLine("PASS "+label); }
        var air=new World(false);
        var floor=new World(true);
        void Frames(PixelPlayer p,int frames)
        { for(int i=0;i<frames;i++) p.Tick(default,air); }
        PixelPlayer Fresh(int capacity)
        { var p=new PixelPlayer(); p.SetDashCapacity(capacity); p.Reset(0,100); return p; }
        Check(PixelPlayer.DashCapacity(false,false)==0,"New save has no dash ability");
        Check(PixelPlayer.DashCapacity(true,false)==1,"Native double jump unlock grants one dash");
        Check(PixelPlayer.DashCapacity(true,true)==2,"Both native unlocks grant two dashes");
        Check(PixelPlayer.DashCapacity(false,true)==2,"Native dash milestone grants two even on unusual old saves");
        var p=Fresh(0);
        p.Tick(new PlayerInput{DashPressed=true,X=1},air);
        Check(!p.DashStarted&&!p.HasDash,"Locked ordinary dash cannot fire");
        p.Tick(new PlayerInput{CrouchDashPressed=true,X=1},air);
        Check(!p.DashStarted&&!p.HasDash,"Locked crouch dash cannot fire");
        p.Reset(0,0); p.Tick(default,floor);
        Check(p.Dashes==0,"Landing does not refill locked dash");
        p.Tick(new PlayerInput{JumpPressed=true,JumpHeld=true},floor);
        Check(p.Jumped,"Basic jump remains available before unlock");
        Frames(p,8); p.Tick(new PlayerInput{JumpPressed=true},air);
        Check(!p.Jumped,"No native double jump is added");
        p=Fresh(0); p.Tick(new PlayerInput{DashPressed=true},air);
        p.SetDashCapacity(1); p.Tick(default,air);
        Check(p.Dashes==1&&!p.DashStarted,"Live unlock grants a charge without replaying a locked press");
        p.Tick(new PlayerInput{DashPressed=true,X=1},air);
        Check(p.DashStarted&&p.Dashes==0,"Single dash consumes exactly one charge");
        Frames(p,30); p.Tick(new PlayerInput{DashPressed=true},air);
        Check(!p.DashStarted,"One-charge milestone rejects a second air dash");
        p.SetDashCapacity(1);
        Check(p.Dashes==0,"Repeated progress reads cannot refill a spent charge");
        p.SetDashCapacity(2);
        Check(p.Dashes==1,"Live second unlock adds only the new charge");
        p=Fresh(2); p.Tick(new PlayerInput{DashPressed=true,X=1},air);
        Check(p.DashStarted&&p.Dashes==1,"First of two dashes leaves one charge");
        Frames(p,30);
        p.Tick(new PlayerInput{CrouchDashPressed=true,Y=1},air);
        Check(p.DashStarted&&p.Dashes==0,"Crouch dash shares the second charge");
        Frames(p,30); p.Tick(new PlayerInput{DashPressed=true},air);
        Check(!p.DashStarted&&p.Dashes==0,"Third air dash is rejected");
        p.Reset(0,100,refillDash:false);
        Check(p.Dashes==0,"Physics handoff preserves spent charges");
        p.Relocate(0,0); p.SetVelocity(0,0); p.Tick(default,floor);
        Check(p.Dashes==2,"Landing restores both charges");
        p.Reset(0,100); p.Tick(new PlayerInput{DashPressed=true,X=1},air);
        p.SetDashCapacity(0); Frames(p,30); p.Tick(new PlayerInput{DashPressed=true},air);
        Check(!p.DashStarted&&p.Dashes==0,"Downgrade removes charges and disallows later dashes");
        p=Fresh(2); p.Tick(new PlayerInput{DashPressed=true,X=1},air);
        Frames(p,1); p.Tick(new PlayerInput{DashPressed=true,Y=1},air);
        Check(!p.DashStarted&&p.Dashes==1,"Second charge does not bypass dash freeze or cooldown");
        Frames(p,30);
        Check(p.Dashes==1,"Too-early second dash expires instead of firing later");

        var feedback=new StatusFeedback();
        feedback.Update(.3f,0,10,.075f,0);
        Check(feedback.UseNativeHair&&!feedback.FlashWhite&&feedback.FlashRed,
            "Locked ability keeps native hair while retaining low stamina warning");
        feedback.Update(.016f,2,110,0,2);
        for(int i=0;i<15;i++) feedback.Update(.016f,2,110,0,2);
        Check(feedback.UseNativeHair&&!feedback.FlashWhite,
            "Full two-charge capacity preserves native hair");
        feedback.Update(.016f,1,110,0,2);
        Check(!feedback.UseNativeHair&&!feedback.FlashWhite&&feedback.R==172f/255&&feedback.G==50f/255,
            "Spending first of two charges displays red without refill flash");
        for(int i=0;i<150;i++) feedback.Update(.016f,0,110,0,2);
        Check(!feedback.UseNativeHair&&Math.Abs(feedback.G-183f/255)<.00001f,
            "Spending second charge displays blue hair");
        feedback.Update(.016f,2,110,0,2);
        Check(feedback.FlashWhite,"Restoring two charges flashes white");
        feedback.Update(.016f,1,110,0,2);
        Check(!feedback.FlashWhite&&!feedback.UseNativeHair&&feedback.R==172f/255,
            "Spending during refill flash immediately shows red");
        feedback=new StatusFeedback();
        feedback.Update(.016f,2,110,0,2);
        Check(feedback.UseNativeHair&&!feedback.FlashWhite,
            "Entering gameplay with two charges starts with native hair");
        Console.WriteLine($"{count} ability progression checks passed.");
    }

    private sealed class World(bool floor):IPixelWorld
    {
        public bool Solid(float x,float y,float w,float h)=>floor&&y<0;
        public bool OneWay(float x,float y,float w,float h,float oldY)=>false;
        public bool OverlapsOneWay(float x,float y,float w,float h)=>false;
        public float? DashPlatformTop(float x,float y,float w,float h)=>null;
    }
}
