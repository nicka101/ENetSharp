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
        internal object SendLock = new object();
        internal ManualResetEventSlim ProceedReliable = new ManualResetEventSlim(true);
        internal object IncomingCommandLock = new object();

        internal ENetSendType SendType;
        internal ushort OutgoingSequenceNumber = 0;
        internal ushort UsedReliableWindows = 0;
        internal ushort ReliableWindows [ENetHost.PEER_RELIABLE_WINDOWS];
        internal ushort IncomingSequenceNumber = 0;
        internal LinkedList<ENetIncomingCommand> IncomingCommands;

        internal ENetChannel(ENetSendType SendType){
            this.SendType = SendType;
        }
    }
}
