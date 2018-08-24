using System;
using System.Threading;

namespace CableRobot.Fins
{
    internal class FinsResponse
    {
        public FinsResponse(byte sid, ushort[] data)
        {
            Sid = sid;
            Data = data;
            WaitEvent = new ManualResetEvent(false);
        }

        public byte Sid { get; private set; }
        public ushort[] Data { get; private set; }
        public ManualResetEvent WaitEvent { get; private set; }
        
        public void Reset()
        {
            Sid = 0;
            Data = null;
            WaitEvent.Reset();
        }
        
        public void PutValue(byte sid, ushort[] data)
        {
            Sid = sid;
            Data = data;
            WaitEvent.Set();
        }
    }
}