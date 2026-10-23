using Doloc.CelesteMovement.Core;

internal static class PixelChecks
{
    internal static void Run()
    {
        int passed=0;
        void Check(bool ok,string name)
        {
            if(!ok) throw new Exception("PIXEL: "+name);
            Console.WriteLine("PASS "+name); passed++;
        }
        var floor=new World().Box(-1000,-20,2000,20);
        var empty=new World();
        PixelPlayer Fresh(World w,float x=0,float y=0)
        {
            var p=new PixelPlayer(); p.Reset(x,y); p.Tick(default,w); return p;
        }
        void Frames(PixelPlayer p,World w,int count,PlayerInput input=default)
        { for(int i=0;i<count;i++) p.Tick(input,w); }
        void Launch(PixelPlayer p,World w,int x,int y,bool demo=false)
        {
            p.Tick(new PlayerInput{X=x,Y=y,DashPressed=!demo,CrouchDashPressed=demo},w);
            int count=0;
            while(p.SpeedX==0&&p.SpeedY==0&&p.Dashing&&count++<10)
                p.Tick(new PlayerInput{X=x,Y=y},w);
            if(count>=10) throw new Exception("Dash never launched");
        }
        var p=Fresh(floor);
        Frames(p,floor,60,new PlayerInput{X=1});
        Check(p.SpeedX==90&&p.Y==0,"Pixel run reaches 90 and remains on floor");
        Frames(p,floor,10);
        Check(p.SpeedX==0,"Pixel run releases to rest");
        float Apex(bool held)
        {
            var a=Fresh(floor); a.Tick(new PlayerInput{JumpPressed=true,JumpHeld=true},floor);
            float top=a.Y;
            for(int i=0;i<90;i++) { a.Tick(new PlayerInput{JumpHeld=held},floor); top=Math.Max(top,a.Y); }
            return top;
        }
        Check(Apex(true)>Apex(false)*1.5,"Pixel variable jump distinguishes tap and hold");
        p=Fresh(floor); p.Relocate(0,3); Frames(p,empty,3);
        p.Tick(new PlayerInput{JumpPressed=true},empty);
        Check(p.Jumped,"Pixel coyote jump");
        p=Fresh(empty,0,4); p.SetVelocity(0,-60);
        p.Tick(new PlayerInput{JumpPressed=true},floor);
        Frames(p,floor,5,new PlayerInput{JumpHeld=true});
        Check(p.SpeedY>0&&p.Y>4,"Pixel buffered landing jump");
        p=Fresh(empty,0,100); float initialX=p.X,initialY=p.Y;
        p.Tick(new PlayerInput{X=1,DashPressed=true},empty);
        Check(p.Frozen&&p.X==initialX&&p.Y==initialY,"Dash starts with motion freeze");
        Frames(p,empty,3,new PlayerInput{X=1});
        Check(!p.Frozen,"Dash freeze is three 60 Hz ticks");
        p.Tick(new PlayerInput{X=1,Y=1},empty);
        Check(Math.Abs(Math.Sqrt(p.SpeedX*p.SpeedX+p.SpeedY*p.SpeedY)-240)<.001,"Dash aim selected after freeze and diagonal normalized");
        Check(!p.HasDash,"Pixel dash consumes charge");
        Frames(p,empty,30);
        p.Tick(new PlayerInput{DashPressed=true},empty);
        Check(!p.DashStarted,"Pixel second air dash rejected");
        p=Fresh(floor); Launch(p,floor,1,0);
        p.Tick(new PlayerInput{X=1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Action=="super"&&p.SpeedX==260&&p.SpeedY==105,"Super exits dash with 260/105 velocity");
        Check(!p.HasDash,"Early super does not refill");
        p=Fresh(floor); Launch(p,floor,1,-1);
        p.Tick(new PlayerInput{X=1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Action=="hyper"&&p.SpeedX==325&&p.SpeedY==52.5f&&!p.Ducking,"Hyper exits crouch slide at 325/52.5");
        p=Fresh(floor); Launch(p,floor,1,-1);
        Frames(p,floor,7,new PlayerInput{X=1,Y=-1});
        p.Tick(new PlayerInput{X=1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Action=="hyper"&&p.HasDash,"Extended hyper refills during dash before jump");
        p=Fresh(floor); Launch(p,floor,1,-1);
        p.Tick(new PlayerInput{X=-1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Action=="hyper"&&p.SpeedX==-325,"Reverse hyper uses updated facing");
        p=Fresh(floor,0,12); Launch(p,floor,1,-1);
        int steps=0;
        while(!p.Grounded&&steps++<12) p.Tick(new PlayerInput{X=1,Y=-1},floor);
        p.Tick(new PlayerInput{X=1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Action=="hyper","Air diagonal dash landing supports wavedash jump");
        var wall=new World().Box(4,-100,20,500);
        p=Fresh(wall,0,40); Launch(p,wall,0,1);
        p.Tick(new PlayerInput{JumpPressed=true,JumpHeld=true},wall);
        Check(p.Action=="wallbounce"&&p.SpeedX==-170&&p.SpeedY==160,"Wallbounce uses 170/160 velocity");
        p=Fresh(wall,0,40);
        p.Tick(new PlayerInput{GrabHeld=true},wall);
        Check(p.State==PlayerMotion.Climb,"Grab enters climbing");
        Frames(p,wall,60,new PlayerInput{GrabHeld=true,Y=1});
        Check(p.Y>60&&p.Stamina<75&&p.SpeedY==45,"Climb rises at 45 and consumes stamina");
        float saved=p.Stamina;
        Frames(p,wall,30,new PlayerInput{GrabHeld=true,Y=-1});
        Check(Math.Abs(p.Stamina-saved)<.01&&p.SpeedY==-80,"Descending costs no stamina");
        p.Tick(new PlayerInput{GrabHeld=true,JumpPressed=true,JumpHeld=true},wall);
        Check(p.Action=="climb-jump"&&p.SpeedY==105,"Climb jump launches vertically");
        float afterJump=p.Stamina;
        p.Tick(new PlayerInput{X=-1,JumpHeld=true},wall);
        Check(Math.Abs(p.Stamina-afterJump)<.01,"Wall boost uses previous-frame movement input");
        p.Tick(new PlayerInput{X=-1,JumpHeld=true},wall);
        Check(p.Stamina>afterJump+27&&p.SpeedX<0,"Wall boost refunds climb jump stamina");
        p=Fresh(wall,0,200);
        p.Tick(new PlayerInput{GrabHeld=true},wall);
        Frames(p,wall,720,new PlayerInput{GrabHeld=true});
        Check(p.State==PlayerMotion.Normal&&p.Stamina<=0,"Exhaustion releases wall");
        var tunnel=new World().Box(-100,-20,200,20).Box(-10,7,20,20);
        p=Fresh(floor); p.Tick(new PlayerInput{Y=-1},floor); p.Tick(default,tunnel);
        Check(p.Ducking&&p.Height==6,"Crouch cannot stand into ceiling");
        p=Fresh(empty,0,40); Launch(p,empty,1,0,true);
        Check(p.Ducking,"Crouch dash preserves short hitbox during dash");
        var platform=new World().Platform(-20,0,40);
        p=Fresh(platform,0,20); Frames(p,platform,60);
        Check(p.Y==0&&p.Grounded,"One-way platform catches falling player");
        p.Tick(new PlayerInput{DropPressed=true},platform); Frames(p,platform,20);
        Check(p.Y<0,"Dedicated drop passes through one-way platform");
        p=Fresh(platform);
        Check(p.TryDropThrough(platform),"Down+jump accepts a supporting one-way platform");
        Frames(p,platform,20,new PlayerInput{Y=-1,JumpHeld=true});
        Check(p.Y<0&&!p.Jumped&&p.SpeedY<0,"Platform drop consumes jump and falls while jump stays held");
        p=Fresh(floor);
        Check(!p.TryDropThrough(floor),"Down+jump never ignores solid ground");
        p.Tick(new PlayerInput{Y=-1,JumpPressed=true,JumpHeld=true},floor);
        Check(p.Jumped&&p.SpeedY>0,"Down+jump on solid ground preserves crouch jump");
        p=Fresh(platform); Launch(p,platform,1,-1);
        Check(!p.TryDropThrough(platform),"Down+jump during dash preserves hyper input");
        var stacked=new World().Platform(-100,0,200).Platform(-100,-60,200);
        p=Fresh(stacked); p.TryDropThrough(stacked); Frames(p,stacked,90);
        Check(p.Grounded&&p.Y==-60,"Drop-through restores collision for lower platforms");
        var closePlatforms=new World().Platform(-100,0,200).Platform(-100,-4,200);
        p=Fresh(closePlatforms); p.TryDropThrough(closePlatforms); Frames(p,closePlatforms,30);
        Check(p.Grounded&&p.Y==-4,"Drop-through catches nearby lower platform before timer expires");
        p=Fresh(empty,0,-15); p.SetVelocity(0,105); Frames(p,platform,5,new PlayerInput{JumpHeld=true});
        Check(p.Y>-15,"Upward travel passes through one-way platform");
        var corner=new World().Box(3,12,20,10);
        p=Fresh(empty,0,0); p.SetVelocity(0,105);
        Frames(p,corner,2);
        Check(p.X<=-1&&p.SpeedY>0,$"Upward corner correction clears small overhang x={p.X} y={p.Y} vy={p.SpeedY}");
        var barrier=new World().Box(10,-100,1,300);
        p=Fresh(empty,0,30); Launch(p,barrier,1,0); Frames(p,barrier,5);
        Check(p.X<=6,"Pixel traversal cannot tunnel through thin wall");
        p=Fresh(floor); p.LiftX=400; p.LiftY=200;
        p.Tick(new PlayerInput{JumpPressed=true},floor);
        Check(p.SpeedX==250&&p.SpeedY==235,"Lift boost caps horizontal 250 and upward 130");
        p=Fresh(wall,0,40);
        p.Tick(new PlayerInput{JumpPressed=true,JumpHeld=true},wall);
        Check(p.Action=="wall-jump"&&p.SpeedX==-130&&p.Facing==1,"Neutral wall jump preserves facing toward wall");
        p=Fresh(empty,0,300);
        Frames(p,empty,90,new PlayerInput{Y=-1});
        Check(p.SpeedY==-240,"Fast fall reaches 240");
        Frames(p,empty,30);
        Check(p.SpeedY==-160,"Releasing down restores normal terminal speed");
        p=Fresh(empty,0,2); Launch(p,floor,1,0);
        Check(p.Y==0,"Horizontal dash snaps down to nearby ground");
        p=Fresh(empty,0,-4); Launch(p,platform,1,0);
        p.Tick(new PlayerInput{X=1},platform);
        Check(p.Y==0,"Horizontal dash lifts onto overlapping one-way platform");
        p=Fresh(floor); p.LiftY=100; p.Tick(default,floor);
        p.Relocate(0,10); p.LiftY=0; p.Tick(default,empty);
        Check(Math.Abs(p.SpeedY-85)<.001,"Walking off rising platform inherits lift before gravity");
        p=Fresh(empty,0,-1);
        Check(p.TryFit(floor)&&p.Y==0&&!p.Overlapping(floor),"Spawn handoff repairs one-pixel floor overlap");
        p=Fresh(empty);
        Check(p.TryFit(tunnel)&&p.Ducking&&!p.Overlapping(tunnel),"Low-ceiling handoff retains crouched controller");
        var embedded=new World().Box(-30,-30,60,60);
        p=Fresh(empty);
        Check(!p.TryFit(embedded)&&p.X==0&&p.Y==0,"Invalid spawn cannot teleport through thick solid");
        Console.WriteLine($"{passed} pixel movement checks passed.");
    }
    private sealed class World:IPixelWorld
    {
        private readonly List<(float x,float y,float w,float h,bool one)> boxes=new();
        internal World Box(float x,float y,float w,float h) { boxes.Add((x,y,w,h,false)); return this; }
        internal World Platform(float x,float y,float w) { boxes.Add((x,y-1,w,1,true)); return this; }
        public bool Solid(float x,float y,float w,float h) => boxes.Any(b=>!b.one&&Overlap(b,x,y,w,h));
        public bool OneWay(float x,float y,float w,float h,float oldY) =>
            boxes.Any(b=>b.one&&oldY>=b.y+b.h&&Overlap(b,x,y,w,h));
        public bool OverlapsOneWay(float x,float y,float w,float h) =>
            boxes.Any(b=>b.one&&Overlap(b,x,y,w,h));
        public float? DashPlatformTop(float x,float y,float w,float h) =>
            boxes.Where(b=>b.one&&Overlap(b,x,y,w,h)&&b.y+b.h>y&&b.y+b.h-y<=6)
                .Select(b=>(float?)(b.y+b.h)).OrderBy(v=>v).FirstOrDefault();
        private static bool Overlap((float x,float y,float w,float h,bool one) b,float x,float y,float w,float h) =>
            x<b.x+b.w&&x+w>b.x&&y<b.y+b.h&&y+h>b.y;
    }
}
