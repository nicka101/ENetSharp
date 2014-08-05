using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public struct ENetChannel
    {
        ushort  OutgoingReliableSequenceNumber = 0;
        ushort  OutgoingUnreliableSequenceNumber = 0;
        ushort  UsedReliableWindows = 0;
        ushort  ReliableWindows [ENET_PEER_RELIABLE_WINDOWS];
        ushort  IncomingReliableSequenceNumber = 0;
        ushort  IncomingUnreliableSequenceNumber = 0;
        ENetList IncomingReliableCommands;
        ENetList IncomingUnreliableCommands;
    }
}
