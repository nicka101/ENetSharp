using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public struct ENetPeer {
        LinkedList<ENetPeer>  dispatchList;
        //Server
        ushort outgoingPeerID;
        ushort incomingPeerID;
        uint sessionID;
        IPEndPoint address;            /**< Internet address of the peer */
        ENetPeerState state;
        List<ENetChannel> channels;
        uint incomingBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
        uint outgoingBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
        uint incomingBandwidthThrottleEpoch;
        uint outgoingBandwidthThrottleEpoch;
        uint incomingDataTotal;
        uint outgoingDataTotal;
        uint lastSendTime;
        uint lastReceiveTime;
        uint nextTimeout;
        uint earliestTimeout;
        uint packetLossEpoch;
        uint packetsSent;
        uint packetsLost;
        uint packetLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
        uint packetLossVariance;
        uint packetThrottle;
        uint packetThrottleLimit;
        uint packetThrottleCounter;
        uint packetThrottleEpoch;
        uint packetThrottleAcceleration;
        uint packetThrottleDeceleration;
        uint packetThrottleInterval;
        uint lastRoundTripTime;
        uint lowestRoundTripTime;
        uint lastRoundTripTimeVariance;
        uint highestRoundTripTimeVariance;
        uint roundTripTime;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
        uint roundTripTimeVariance;
        ushort mtu;
        uint windowSize;
        uint reliableDataInTransit;
        ushort outgoingReliableSequenceNumber;
        ENetList acknowledgements;
        ENetList sentReliableCommands;
        ENetList sentUnreliableCommands;
        ENetList outgoingReliableCommands;
        ENetList outgoingUnreliableCommands;
        ENetList dispatchedCommands;
        bool needsDispatch;
        ushort incomingUnsequencedGroup;
        ushort outgoingUnsequencedGroup;
        uint unsequencedWindow [ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32]; 
        uint disconnectData;
    }
}
