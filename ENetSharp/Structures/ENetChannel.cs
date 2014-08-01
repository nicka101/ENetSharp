using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public struct ENetChannel
    {
        ushort  OutgoingReliableSequenceNumber;
        ushort  OutgoingUnreliableSequenceNumber;
        ushort  UsedReliableWindows;
        ushort  ReliableWindows [ENET_PEER_RELIABLE_WINDOWS];
        ushort  IncomingReliableSequenceNumber;
        ushort  IncomingUnreliableSequenceNumber;
        ENetList IncomingReliableCommands;
        ENetList IncomingUnreliableCommands;
    }
}
