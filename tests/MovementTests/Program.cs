using Doloc.CelesteMovement.Core;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
const float dt = 1f / 60;
var ground = new MotionEnvironment { Grounded = true };
var air = new MotionEnvironment();
var motor = new MovementMotor();
for (int i=0;i<60;i++) motor.Step(dt, new MotionInput { X = 1 }, ground);
Check(Math.Abs(motor.X - 90) < .001, "Ground run reaches 90 px/s");
for (int i=0;i<10;i++) motor.Step(dt, default, ground);
Check(motor.X == 0, "Ground release stops the player");
motor.Reset(200,0);
motor.Step(dt, new MotionInput { X=1 }, ground);
Check(Math.Abs(motor.X - (200 - 400*dt)) < .01, "Overspeed retains momentum with slower reduction");
motor.Reset(0,0); motor.Step(dt, default, ground);
for(int i=0;i<3;i++) motor.Step(dt, default, air);
motor.Step(dt, new MotionInput { JumpPressed=true, JumpHeld=true }, air);
Check(motor.Jumped && motor.Y == 105, "Coyote jump succeeds within grace window");
motor.Reset(0,0); motor.Step(dt, default, ground);
for(int i=0;i<8;i++) motor.Step(dt, default, air);
motor.Step(dt, new MotionInput { JumpPressed=true }, air);
Check(!motor.Jumped, "Coyote jump expires");
motor.Reset(0,-100); motor.Step(dt, new MotionInput { JumpPressed=true }, air);
motor.Step(dt, default, air); motor.Step(dt, new MotionInput { JumpHeld=true }, ground);
Check(motor.Jumped, "Buffered jump fires on landing");
float Apex(bool held, float step) {
    var m=new MovementMotor(); m.Step(step,new MotionInput{JumpPressed=true,JumpHeld=true},ground);
    float y=0,max=0;
    for(int i=0;i<(int)(2/step);i++){ y+=m.Y*step; max=Math.Max(max,y); m.Step(step,new MotionInput{JumpHeld=held},air); }
    return max;
}
Check(Apex(true,dt) > Apex(false,dt)*1.5f, "Holding jump produces a substantially higher apex");
Check(Math.Abs(Apex(true,1f/50)-Apex(true,1f/120)) < 3, "Jump height remains comparable at 50 and 120 Hz");
motor.Reset(0,0); motor.Step(dt,new MotionInput{X=1,Y=1,DashPressed=true},air);
Check(Math.Abs(Math.Sqrt(motor.X*motor.X+motor.Y*motor.Y)-240)<.01,"Diagonal dash is normalized");
Check(!motor.HasDash && motor.Dashing,"Dash consumes charge");
for(int i=0;i<20;i++) motor.Step(dt,default,air);
motor.Step(dt,new MotionInput{DashPressed=true},air);
Check(!motor.DashStarted,"No second air dash before landing");
motor.ClearInputBuffers(); motor.Step(dt,default,ground);
Check(motor.HasDash,"Landing refills dash");
motor.Reset(0,0); motor.Step(dt,new MotionInput{JumpPressed=true,JumpHeld=true},ground);
motor.ObserveVelocity(0,0); motor.Step(dt,new MotionInput{JumpHeld=true},new MotionEnvironment{Ceiling=true});
Check(motor.Y<=0,"Ceiling collision cancels variable jump");
motor.Reset(0,-20); motor.Step(dt,new MotionInput{X=1,JumpPressed=true},new MotionEnvironment{WallRight=true});
Check(motor.WallJumped && motor.X == -130 && motor.Y ==105,"Wall jump pushes away from wall");
motor.Reset(0,0); motor.Step(dt,new MotionInput{X=1,DashPressed=true},air);
motor.ObserveVelocity(0,0); motor.Step(dt,default,new MotionEnvironment{WallRight=true});
Check(motor.X==0,"Dash respects wall collision response");
motor.Reset(0,0); motor.Step(dt,new MotionInput{JumpPressed=true},air); motor.ClearInputBuffers(); motor.Step(dt,default,ground);
Check(!motor.Jumped,"Clearing input prevents menu or transition buffered jump");
Console.WriteLine($"{passed} movement checks passed.");
PixelChecks.Run();
AnimationChecks.Run();
StatusChecks.Run();
WorldReadinessChecks.Run();
AbilityChecks.Run();
