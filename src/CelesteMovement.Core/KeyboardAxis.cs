namespace Doloc.CelesteMovement.Core
{
    // Celeste's TakeNewer keyboard rule: the second opposing direction wins.
    // Pressing both from neutral stays neutral until one is released.
    public sealed class KeyboardAxis
    {
        private int value;
        private bool turned;
        public int Update(bool negative,bool positive)
        {
            if(negative&&positive)
            {
                if(!turned) { value=-value; turned=true; }
            }
            else
            {
                turned=false;
                value=negative?-1:positive?1:0;
            }
            return value;
        }
        public void Clear() { value=0; turned=false; }
    }
}
