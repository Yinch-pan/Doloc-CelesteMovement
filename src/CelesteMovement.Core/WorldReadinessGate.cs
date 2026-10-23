#nullable enable
namespace Doloc.CelesteMovement.Core
{
    // DTMAPI 0.7.0 can keep LoadingWorld after a transition without a matching
    // room-enter observation. Only recover within an already validated save.
    public sealed class WorldReadinessGate
    {
        private long? readySave;
        private long revision=-1;
        private object? room, actor;
        private float since;
        private int firstFrame;
        public bool UsedFallback { get; private set; }

        public void Reset()
        {
            readySave=null; revision=-1; room=actor=null; UsedFallback=false;
        }

        public bool Observe(bool worldReady,bool loadingWorld,long? save,long contextRevision,
            bool nativeReady,object? currentRoom,object? currentActor,float now,int frame)
        {
            UsedFallback=false;
            if(save!=readySave) { readySave=null; room=actor=null; }
            if(worldReady)
            {
                readySave=save; room=actor=null;
                return nativeReady;
            }
            if(!loadingWorld||!save.HasValue||readySave!=save)
            {
                // Title/loading-save boundaries invalidate prior evidence even
                // if a caller accidentally reuses the same save identifier.
                readySave=null; room=actor=null;
                return false;
            }
            if(!nativeReady||currentRoom==null||currentActor==null)
            {
                room=actor=null;
                return false;
            }
            if(revision!=contextRevision||!ReferenceEquals(room,currentRoom)||!ReferenceEquals(actor,currentActor))
            {
                revision=contextRevision; room=currentRoom; actor=currentActor;
                since=now; firstFrame=frame;
                return false;
            }
            UsedFallback=frame>firstFrame&&now-since>=.25f;
            return UsedFallback;
        }
    }
}
