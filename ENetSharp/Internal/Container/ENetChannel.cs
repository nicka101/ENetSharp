using ENetSharp.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Container
{
    internal class ENetChannel
    {
        internal Object SendLock = new Object();
        internal ManualResetEventSlim ProceedReliable = new ManualResetEventSlim(true);
        internal ENetSendType SendType;
        internal ushort OutgoingSequenceNumber = 0;
        internal ushort UsedReliableWindows = 0;
        internal ushort ReliableWindows [ENET_PEER_RELIABLE_WINDOWS];
        internal ushort IncomingSequenceNumber = 0;
        ENetList IncomingCommands;

        internal ENetChannel(ENetSendType SendType){
            this.SendType = SendType;
        }
    }
}
