using ENetSharp.Container;
using ENetSharp.Internal;
using ENetSharp.Internal.Container;
using ENetSharp.Internal.Protocol;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Protocol
{
    public unsafe struct ENetPeer {
        internal const ushort PROTOCOL_MINIMUM_MTU = 576;
        internal const ushort PROTOCOL_MAXIMUM_MTU = 996;
        internal const ushort PROTOCOL_MAXIMUM_WINDOW_SIZE = 32768;

        private UdpClient sendConnection = new UdpClient();
        private Object sendLock = new Object();

        //internal LinkedList<ENetPeer>  DispatchList;
        //Server
        internal ushort OutgoingPeerID;
        internal ushort IncomingPeerID;
        internal readonly uint SessionID;
        public readonly IPEndPoint Address;            /**< Internet address of the peer */
        public ENetPeerState State = ENetPeerState.ACKNOWLEDGING_CONNECT;
        internal ENetChannel[] Channels;
        //internal uint IncomingBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
        //internal uint OutgoingBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
        //internal uint IncomingBandwidthThrottleEpoch;
        //internal uint OutgoingBandwidthThrottleEpoch;
        //internal uint IncomingDataTotal;
        //internal uint OutgoingDataTotal;
        internal uint LastSendTime = 0;
        internal uint LastReceiveTime = 0;
        internal uint NextTimeout = 0;
        internal uint EarliestTimeout = 0;
        internal uint PacketLossEpoch = 0;
        internal uint PacketsSent = 0;
        internal uint PacketsLost = 0;
        internal uint PacketLoss = 0;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
        internal uint PacketLossVariance = 0;
        //internal uint PacketThrottle;
        //internal uint PacketThrottleLimit;
        //internal uint PacketThrottleCounter;
        //internal uint PacketThrottleEpoch;
        //internal uint PacketThrottleAcceleration;
        //internal uint PacketThrottleDeceleration;
        //internal uint PacketThrottleInterval;
        //internal uint LastRoundTripTime;
        internal uint LowestRoundTripTime = 0;
        internal uint LastRoundTripTimeVariance = 0;
        internal uint HighestRoundTripTimeVariance = 0;
        internal uint RoundTripTime = 0;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
        internal uint RoundTripTimeVariance = 0;
        internal readonly ushort MTU;
        internal readonly int FragmentLength;
        internal uint WindowSize;
        internal uint ReliableDataInTransit = 0;
        internal ushort OutgoingReliableSequenceNumber = 0;
        internal ENetList SentReliableCommands;
        internal ENetList SentUnreliableCommands;
        internal ENetList OutgoingReliableCommands;
        internal ENetList OutgoingUnreliableCommands;
        internal ENetList DispatchedCommands;
        internal ConcurrentQueue<ENetProtocolAcknowledge> PendingAcks = new ConcurrentQueue<ENetProtocolAcknowledge>();
        internal bool NeedsDispatch = false;
        internal ushort IncomingUnsequencedGroup = 0;
        internal ushort OutgoingUnsequencedGroup = 0;
        internal fixed uint UnsequencedWindow [ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32]; 
        internal uint DisconnectData = 0;

        internal ENetPeer(IPEndPoint Address, ushort IncomingPeerID, ENetProtocolConnect packet, ENetChannelTypeLayout channelLayout){
            this.Address = Address;
            this.SessionID = packet.SessionID;
            this.MTU = packet.MTU < PROTOCOL_MINIMUM_MTU ? PROTOCOL_MINIMUM_MTU : packet.MTU > PROTOCOL_MAXIMUM_MTU ? PROTOCOL_MAXIMUM_MTU : packet.MTU;
            this.FragmentLength = (MTU - ENetCommand.SEND_FRAGMENT.Size()) - sizeof(ENetProtocolHeader);
            this.OutgoingPeerID = packet.OutgoingPeerID;
            this.IncomingPeerID = IncomingPeerID;
            this.WindowSize = PROTOCOL_MAXIMUM_WINDOW_SIZE > packet.WindowSize ? packet.WindowSize : PROTOCOL_MAXIMUM_WINDOW_SIZE;
            this.Channels = new ENetChannel[channelLayout.ChannelCount()];
            for(byte i = 0; i < Channels.Length; i++){
                Channels[i] = new ENetChannel(channelLayout[i]);
            }
        }

        internal void SendRaw(byte[] data){
            lock(sendLock){
                sendConnection.BeginSend(data, data.Length, Address, EndSend, data.Length);
            }
        }

        internal void SendENet(byte[] enetPacket){
            byte[] data = new byte[ENetCommand.PROTOCOL_HEADER.Size() + enetPacket.Length];
            ENetProtocolHeader header = new ENetProtocolHeader { PeerID = Util.ToNetOrder(OutgoingPeerID), SentTime = Util.ToNetOrder(Util.Timestamp()) };
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            Marshal.StructureToPtr(header, handle.AddrOfPinnedObject(), false);
            handle.Free();
            Buffer.BlockCopy(enetPacket, 0, data, ENetCommand.PROTOCOL_HEADER.Size(), enetPacket.Length);
            SendRaw(data);
        }

        internal void SendAcks(){
            if(State == ENetPeerState.DISCONNECTED || State == ENetPeerState.ZOMBIE) return;
            ENetProtocolAcknowledge ack;
            int headerSize = sizeof(ENetProtocolHeader);
            int ackSize = ENetCommand.ACKNOWLEDGE.Size();
            byte[] data = new byte[headerSize + (PendingAcks.Count * ackSize)];
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var currentLoc = handle.AddrOfPinnedObject() + headerSize;
            while (PendingAcks.TryDequeue(out ack))
            {
                Marshal.StructureToPtr(ack, currentLoc, false);
                currentLoc += ackSize;
            }
            ENetProtocolHeader header = new ENetProtocolHeader { PeerID = Util.ToNetOrder(OutgoingPeerID), SentTime = Util.ToNetOrder(Util.Timestamp()) };
            Marshal.StructureToPtr(header, handle.AddrOfPinnedObject(), false);
            handle.Free();
            SendRaw(data);
        }

        public bool Send(byte channelID, ENetPacket packet){
            if(State != ENetPeerState.CONNECTED || channelID > Channels.Length) return false;
            if(packet.Data.Length > FragmentLength){ //Packet bigger than MTU, fragment it
                ushort sequenceNo = Util.ToNetOrder((ushort)(OutgoingReliableSequenceNumber + 1));
                uint fragCount = Util.ToNetOrder((uint)((packet.Data.Length + FragmentLength - 1) / FragmentLength));

                for(int fragmentNo = 0, fragmentOffset = 0; fragmentOffset < packet.Data.Length; fragmentNo++, fragmentOffset += FragmentLength){
                    if (packet.Data.Length - fragmentOffset < FragmentLength) FragmentLength = packet.Data.Length - fragmentOffset;
                }
            }
        }

        internal void EndSend(IAsyncResult res){
            int bytesSent = sendConnection.EndSend(res);
            if(bytesSent < (int)res.AsyncState) throw new Exception(String.Format("ENetPeer failed to send {0} bytes to peer {1}", ((int)res.AsyncState) - bytesSent, Address.ToString()));
        }
    }
}
