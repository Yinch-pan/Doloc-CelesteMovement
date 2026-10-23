using Doloc.CelesteMovement.Core;

internal static class AnimationChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; Console.WriteLine("PASS "+name); }
        var pose=new AnimationMotion();
        var seen=new HashSet<int>();
        for(int i=0;i<8;i++) { pose.Update(1f/60,true,true,false,false,0,0); seen.Add(pose.CrouchFrame); }
        Check(seen.SetEquals(new[]{0,1,2,3}),"Crouch entry traverses all four native frames");
        seen.Clear();
        for(int i=0;i<150;i++) { pose.Update(1f/60,true,true,false,false,0,0); seen.Add(pose.CrouchFrame); }
        Check(seen.SetEquals(new[]{0,1}),"Crouch hold breathes without standing up");
        seen.Clear();
        for(int i=0;i<30;i++) { pose.Update(1f/60,true,true,true,false,240,0); seen.Add(pose.CrouchFrame); }
        Check(seen.SetEquals(new[]{0,1}),"Crouch slide cycles low poses");
        seen.Clear();
        for(int i=0;i<8;i++) { pose.Update(1f/60,false,true,false,false,0,0); seen.Add(pose.CrouchFrame); }
        Check(!pose.CrouchPose&&seen.Contains(1)&&seen.Contains(2)&&seen.Contains(3),"Standing up completes and releases the crouch pose");
        for(int i=0;i<8;i++) pose.Update(1f/60,true,true,false,false,0,0);
        pose.Update(1f/60,false,false,false,false,0,105);
        Check(!pose.CrouchPose,"Jump immediately interrupts the standing transition");
        for(int i=0;i<12;i++) pose.Update(1f/60,false,false,false,true,0,45);
        float before=pose.Phase;
        pose.Update(1f/60,false,false,false,true,0,-45);
        Check(pose.Phase<before,"Downward climbing reverses the gait");
        before=pose.Phase;
        for(int i=0;i<20;i++) pose.Update(1f/60,false,false,false,true,0,0);
        Check(pose.Phase==before&&pose.Effort==0,"Holding a wall stops the stepping cycle");
        var fast=new AnimationMotion(); var slow=new AnimationMotion();
        fast.Update(.1f,false,false,false,true,0,40);
        slow.Update(.1f,false,false,false,true,0,20);
        Check(Math.Abs(fast.Phase-slow.Phase*2)<.00001f,"Climb cadence follows vertical travel");
        var up=new AnimationMotion();
        up.Update(.1f,false,false,false,true,0,40);
        up.Update(.1f,false,false,false,true,0,40); // quarter cycle, full effort
        up.Offset(.25f,.15f,false,out _,out float left);
        up.Offset(.8f,.15f,false,out _,out float right);
        Check(left>1&&right< -1,"Climbing feet alternate rather than translating together");
        up.Offset(.3f,.9f,false,out float headX,out float headY);
        up.Offset(.9f,.4f,true,out float hatX,out float hatY);
        Check(Math.Abs(headX-hatX)<.00001&&Math.Abs(headY-hatY)<.00001,"Hair and hat follow the head offset");
        float phase=up.Phase;
        up.Update(0,false,false,false,true,0,45);
        Check(up.Phase==phase,"Paused time does not advance animation");
        up.Update(.01f,false,false,true,false,240,0);
        Check(!up.Climbing&&up.Effort==0,"Dash clears the climb rig state");
        Check(float.IsFinite(left)&&float.IsFinite(right),"Rig offsets remain finite");
        Console.WriteLine($"{count} animation checks passed.");
    }
}
