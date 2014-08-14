using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ENetProtocolSendFragment
    {
        public ENetProtocolCommandHeader Header;
        public ushort StartSequenceNumber;
        public ushort DataLength;
        public uint FragmentCount;
        public uint FragmentNumber;
        public uint TotalLength;
        public uint FragmentOffset;
    }
}
