using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ENetProtocolThrottleConfigure
    {
        public ENetProtocolCommandHeader Header;
        public uint PacketThrottleInterval;
        public uint PacketThrottleAcceleration;
        public uint PacketThrottleDeceleration;
    }
}
