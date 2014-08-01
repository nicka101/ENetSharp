using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ENetProtocolCommandHeader
    {
        public byte Command;
        public byte ChannelID;
        public ushort ReliableSequenceNumber;
    }
}
