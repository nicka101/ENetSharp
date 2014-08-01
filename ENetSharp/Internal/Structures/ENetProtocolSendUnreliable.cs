using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ENetProtocolSendUnreliable
    {
        public ENetProtocolCommandHeader Header;
        public ushort UnreliableSequenceNumber;
        public ushort DataLength;
    }
}
