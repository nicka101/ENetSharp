using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ENetProtocolAcknowledge
    {
        public ENetProtocolCommandHeader Header;
        public ushort ReceivedReliableSequenceNumber;
        public ushort ReceivedSentTime;
    }
}
