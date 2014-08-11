using ENetSharp.Internal.Structures;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public unsafe struct ENetPeer {
        internal const ushort PROTOCOL_MINIMUM_MTU = 576;
        internal const ushort PROTOCOL_MAXIMUM_MTU = 996;
        internal const ushort PROTOCOL_MAXIMUM_WINDOW_SIZE = 32768;

        internal UdpClient sendConnection = new UdpClient();

        //internal LinkedList<ENetPeer>  DispatchList;
        //Server
        internal ushort OutgoingPeerID;
        internal ushort IncomingPeerID;
        internal readonly uint SessionID;
        public readonly IPEndPoint Address;            /**< Internet address of the peer */
        public ENetPeerState State = ENetPeerState.ACKNOWLEDGING_CONNECT;
        internal ConcurrentBag<ENetChannel> Channels = new ConcurrentBag<ENetChannel>();
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
        internal ENetList Acknowledgements;
        internal ENetList SentReliableCommands;
        internal ENetList SentUnreliableCommands;
        internal ENetList OutgoingReliableCommands;
        internal ENetList OutgoingUnreliableCommands;
        internal ENetList DispatchedCommands;
        internal bool NeedsDispatch = false;
        internal ushort IncomingUnsequencedGroup = 0;
        internal ushort OutgoingUnsequencedGroup = 0;
        internal uint UnsequencedWindow [ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32]; 
        internal uint DisconnectData = 0;

        internal ENetPeer(IPEndPoint Address, ushort IncomingPeerID, ENetProtocolConnect packet){
            this.Address = Address;
            this.SessionID = packet.SessionID;
            this.MTU = packet.MTU < PROTOCOL_MINIMUM_MTU ? PROTOCOL_MINIMUM_MTU : packet.MTU > PROTOCOL_MAXIMUM_MTU ? PROTOCOL_MAXIMUM_MTU : packet.MTU;
            this.FragmentLength = (MTU - ENetCommand.SEND_FRAGMENT.Size()) - sizeof(ENetProtocolHeader);
            this.OutgoingPeerID = packet.OutgoingPeerID;
            this.IncomingPeerID = IncomingPeerID;
            this.WindowSize = PROTOCOL_MAXIMUM_WINDOW_SIZE > packet.WindowSize ? packet.WindowSize : PROTOCOL_MAXIMUM_WINDOW_SIZE;
        }

        internal void SendRaw(byte[] data){
            sendConnection.BeginSend(data, data.Length, Address, EndSend, data.Length);
        }

        public bool Send(byte channelID, ENetPacket packet){
            if(State != ENetPeerState.CONNECTED || channelID > Channels.Count) return false;
            if(packet.Data.Length > FragmentLength){
                ushort sequenceNo = (ushort)IPAddress.HostToNetworkOrder(unchecked((Int16)OutgoingReliableSequenceNumber + 1));
                uint fragCount = (uint)IPAddress.HostToNetworkOrder((packet.Data.Length + FragmentLength - 1) / FragmentLength);
            }
        }

        internal void EndSend(IAsyncResult res){
            int bytesSent = sendConnection.EndSend(res);
            if(bytesSent < (int)res.AsyncState) throw new Exception(String.Format("ENetPeer failed to send {0} bytes to peer {1}", ((int)res.AsyncState) - bytesSent, Address.ToString()));
        }
    }
}
