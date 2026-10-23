using Doloc.CelesteMovement.Core;

internal static class WorldReadinessChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string label) { if(!ok) throw new Exception(label); count++; Console.WriteLine("PASS "+label); }
        var gate=new WorldReadinessGate();
        var room=new object(); var actor=new object();
        bool Observe(bool ready,bool loading,long? save,long revision,bool native,float time,int frame) =>
            gate.Observe(ready,loading,save,revision,native,room,actor,time,frame);
        Check(!Observe(false,true,1,1,true,0,0)&&!Observe(false,true,1,1,true,10,600),
            "Initial load cannot bypass the first WorldReady");
        Check(Observe(true,false,1,2,true,11,660)&&!gate.UsedFallback,"Official readiness enables movement immediately");
        Check(!Observe(false,true,1,3,false,12,720),"Actual transition releases movement");
        Check(!Observe(false,true,1,3,true,13,780),"Native resume requires a stable observation interval");
        Check(!Observe(false,true,1,3,true,13.2f,792),"Transient native readiness does not enable movement");
        Check(Observe(false,true,1,3,true,13.3f,798)&&gate.UsedFallback,"Stale LoadingWorld recovers after native gameplay is stable");
        Check(!Observe(false,true,1,3,false,14,840),"Pause/cutscene/riding evidence blocks even after recovery");
        Observe(false,true,1,3,true,15,900);
        room=new object();
        Check(!Observe(false,true,1,3,true,16,960),"Changing rooms invalidates prior stability");
        Check(Observe(false,true,1,3,true,16.3f,978),"The new rendered room can recover");
        Check(!Observe(false,true,1,4,true,17,1020),"A new transition revision requires new stability");
        actor=new object();
        Check(!Observe(false,true,1,4,true,18,1080),"Replacing the player invalidates prior stability");
        Check(!Observe(false,true,1,4,true,19,1080),"Repeated checks in one frame do not count as stable gameplay");
        Check(Observe(false,true,1,4,true,19.1f,1081),"Distinct stable native frames enable recovery");
        Check(!Observe(false,true,2,5,true,20,1200)&&!Observe(false,true,2,5,true,30,1800),
            "A different save cannot reuse the previous save readiness");
        Observe(true,false,2,6,true,31,1860);
        Observe(false,false,null,7,false,32,1920);
        Check(!Observe(false,true,2,8,true,33,1980),"Returning to title invalidates readiness");
        Observe(true,false,2,9,true,34,2040); gate.Reset();
        Check(!Observe(false,true,2,10,true,35,2100),"Runtime reset invalidates readiness");
        Console.WriteLine($"{count} world readiness checks passed.");
    }
}
