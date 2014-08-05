using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public struct ENetPeer {
        internal LinkedList<ENetPeer>  DispatchList;
        //Server
        internal ushort OutgoingPeerID;
        internal ushort IncomingPeerID;
        internal uint SessionID;
        public IPEndPoint Address;            /**< Internet address of the peer */
        public ENetPeerState State;
        internal List<ENetChannel> Channels;
        internal uint IncomingBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
        internal uint OutgoingBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
        internal uint IncomingBandwidthThrottleEpoch;
        internal uint OutgoingBandwidthThrottleEpoch;
        internal uint IncomingDataTotal;
        internal uint OutgoingDataTotal;
        internal uint LastSendTime;
        internal uint LastReceiveTime;
        internal uint NextTimeout;
        internal uint EarliestTimeout;
        internal uint PacketLossEpoch;
        internal uint PacketsSent;
        internal uint PacketsLost;
        internal uint PacketLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
        internal uint PacketLossVariance;
        internal uint PacketThrottle;
        internal uint PacketThrottleLimit;
        internal uint PacketThrottleCounter;
        internal uint PacketThrottleEpoch;
        internal uint PacketThrottleAcceleration;
        internal uint PacketThrottleDeceleration;
        internal uint PacketThrottleInterval;
        internal uint LastRoundTripTime;
        internal uint LowestRoundTripTime;
        internal uint LastRoundTripTimeVariance;
        internal uint HighestRoundTripTimeVariance;
        internal uint RoundTripTime;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
        internal uint RoundTripTimeVariance;
        internal ushort MTU;
        internal uint WindowSize;
        internal uint ReliableDataInTransit;
        internal ushort OutgoingReliableSequenceNumber;
        internal ENetList Acknowledgements;
        internal ENetList SentReliableCommands;
        internal ENetList SentUnreliableCommands;
        internal ENetList OutgoingReliableCommands;
        internal ENetList OutgoingUnreliableCommands;
        internal ENetList DispatchedCommands;
        internal bool NeedsDispatch;
        internal ushort IncomingUnsequencedGroup;
        internal ushort OutgoingUnsequencedGroup;
        internal uint UnsequencedWindow [ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32]; 
        internal uint DisconnectData;
    }
}
