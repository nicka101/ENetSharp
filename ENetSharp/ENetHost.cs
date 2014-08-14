using ENetSharp.Container;
using ENetSharp.Internal.Protocol;
using ENetSharp.Protocol;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ENetSharp
{
    public class ENetHost : IDisposable
    {
        internal const ushort PROTOCOL_HEADER_FLAG_MASK = 0x2980;
        internal const ushort PROTOCOL_HEADER_FLAG_SENT_TIME = 0x80;
        internal const byte PROTOCOL_MINIMUM_CHANNEL_COUNT = 1;
        internal const byte PROTOCOL_MAXIMUM_CHANNEL_COUNT = 255;
        internal const int PROTOCOL_MAXIMUM_PEER_ID = 0x007F;

        private UdpClient connection;
        private bool shuttingDown = false;
        private ManualResetEventSlim shutdownComplete = new ManualResetEventSlim(false);
        private readonly ushort PeerCount;
        private ConcurrentDictionary<ushort, ENetPeer> Peers;
        private ConcurrentQueue<ushort> AvailablePeerIds = new ConcurrentQueue<ushort>();
        internal readonly ENetChannelTypeLayout ChannelLayout;

        public delegate void ConnectHandler(ENetPeer peer);
        public delegate void DisconnectHandler(ENetPeer peer);
        public delegate void DataHandler(ENetPeer peer, byte[] data);

        public event ConnectHandler OnConnect;
        public event DisconnectHandler OnDisconnect;
        public event DataHandler OnData;

        public ENetHost(IPEndPoint listenAddress, ushort peerCount, ENetChannelTypeLayout channelLayout)
        {
            if (peerCount > PROTOCOL_MAXIMUM_PEER_ID) throw new ArgumentException("The given peer count exceeds the protocol maximum of " + PROTOCOL_MAXIMUM_PEER_ID);
            PeerCount = peerCount;
            ChannelLayout = channelLayout;
            connection = new UdpClient(listenAddress);
            for (ushort i = 1; i <= peerCount; i++)
            {
                AvailablePeerIds.Enqueue(i);
            }
        }

        public void Start()
        {
            connection.BeginReceive(ReceiveDatagram, null);
        }

        public void Dispose()
        {
            shuttingDown = true;
            shutdownComplete.Wait();
        }

        internal unsafe void ReceiveDatagram(IAsyncResult ar)
        {
            IPEndPoint fromAddr = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = connection.EndReceive(ar, ref fromAddr);
            if (!shuttingDown) connection.BeginReceive(ReceiveDatagram, null);

            #region "ENet Structure Handling"
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr dataStart = handle.AddrOfPinnedObject();
            ENetProtocolHeader header = (ENetProtocolHeader)Marshal.PtrToStructure(dataStart, typeof(ENetProtocolHeader));
            header.PeerID = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)header.PeerID));
            ushort flag = (ushort)(header.PeerID & PROTOCOL_HEADER_FLAG_MASK);
            header.PeerID &= unchecked((ushort)~(uint)PROTOCOL_HEADER_FLAG_MASK);

            Nullable<ENetPeer> peer = null;
            if (header.PeerID != PROTOCOL_MAXIMUM_PEER_ID) //peer remains null if the first command is expected to be a connect
            {
                try
                {
                    peer = Peers[header.PeerID];
                    if (peer.Value.State == ENetPeerState.DISCONNECTED || 
                        peer.Value.State == ENetPeerState.ZOMBIE ||
                        //Don't include ENET_HOST_BROADCAST, it's meant for clients broadcasting the connect packet and communicating with any server that responds
                        peer.Value.Address != fromAddr /* && peer.Value.Address != ENET_HOST_BROADCAST */)
                    {
                        goto finalPacket; //The peer is disconnected, dead or the packets origin doesn't match the peer - Ignore them
                    }
                }
                catch (KeyNotFoundException)
                {
                    goto finalPacket; //The client doesn't exist and this doesn't follow connection protocol - Ignore them
                }
            }
            int currentDataOffset = ((flag & PROTOCOL_HEADER_FLAG_SENT_TIME) != 0) ? sizeof(ENetProtocolHeader) : sizeof(ENetProtocolHeader) - 2; //sentTime is 2 bytes
            while (currentDataOffset < data.Length)
            {
                ENetProtocol packet = (ENetProtocol)Marshal.PtrToStructure(dataStart + currentDataOffset, typeof(ENetProtocol));
                ToHostOrder(ref packet.Header.ReliableSequenceNumber);
                ENetCommand command = (ENetCommand)(packet.Header.Command & (byte)ENetCommand.ENET_PROTOCOL_COMMAND_MASK);
                if(command >= ENetCommand.COUNT) continue;
                if (peer == null && command != ENetCommand.CONNECT) return; //Peer was following connection protocol but didn't send the connect first
                currentDataOffset += command.Size();
                if (currentDataOffset > data.Length) return; //The ENetCommand is larger than the remaining data
                switch (command)
                {
                    case ENetCommand.ACKNOWLEDGE:
                        ToHostOrder(ref packet.Acknowledge.ReceivedReliableSequenceNumber);
                        ToHostOrder(ref packet.Acknowledge.ReceivedSentTime);
                        //TODO: Handle Acknowledge
                        break;
                    case ENetCommand.BANDWIDTH_LIMIT:
                        ToHostOrder(ref packet.BandwidthLimit.IncomingBandwidth);
                        ToHostOrder(ref packet.BandwidthLimit.OutgoingBandwidth);
                        //TODO: Handle Bandwidth Limit
                        break;
                    case ENetCommand.CONNECT:
                        ToHostOrder(ref packet.Connect.MTU);
                        ToHostOrder(ref packet.Connect.WindowSize);
                        ToHostOrder(ref packet.Connect.ChannelCount);
                        ToHostOrder(ref packet.Connect.IncomingBandwidth);
                        ToHostOrder(ref packet.Connect.OutgoingBandwidth);
                        ToHostOrder(ref packet.Connect.PacketThrottleInterval);
                        ToHostOrder(ref packet.Connect.PacketThrottleAcceleration);
                        ToHostOrder(ref packet.Connect.PacketThrottleDeceleration);
                        ToHostOrder(ref packet.Connect.SessionID);
                        peer = HandleConnect(fromAddr, packet.Connect);
                        break;
                    case ENetCommand.DISCONNECT:
                        ToHostOrder(ref packet.Disconnect.Data);
                        //TODO: Handle Disconnect
                        break;
                    case ENetCommand.PING:
                        //TODO: Handle Ping
                        break;
                    case ENetCommand.SEND_FRAGMENT:
                        ToHostOrder(ref packet.SendFragment.StartSequenceNumber);
                        ToHostOrder(ref packet.SendFragment.DataLength);
                        ToHostOrder(ref packet.SendFragment.FragmentCount);
                        ToHostOrder(ref packet.SendFragment.FragmentNumber);
                        ToHostOrder(ref packet.SendFragment.TotalLength);
                        ToHostOrder(ref packet.SendFragment.FragmentOffset);
                        currentDataOffset += packet.SendFragment.DataLength;
                        //TODO: Handle Send Fragment
                        break;
                    case ENetCommand.SEND_RELIABLE:
                        ToHostOrder(ref packet.SendReliable.DataLength);
                        //TODO: Handle Send Reliable
                        currentDataOffset += packet.SendReliable.DataLength;
                        break;
                    case ENetCommand.SEND_UNRELIABLE:
                        ToHostOrder(ref packet.SendUnreliable.UnreliableSequenceNumber);
                        ToHostOrder(ref packet.SendUnreliable.DataLength);
                        currentDataOffset += packet.SendUnreliable.DataLength;
                        //TODO: Handle Send Unreliable
                        break;
                    case ENetCommand.SEND_UNSEQUENCED:
                        ToHostOrder(ref packet.SendUnsequenced.UnsequencedGroup);
                        ToHostOrder(ref packet.SendUnsequenced.DataLength);
                        currentDataOffset += packet.SendUnsequenced.DataLength;
                        //TODO: Handle Send Unsequenced
                        break;
                    case ENetCommand.THROTTLE_CONFIGURE:
                        ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleInterval);
                        ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleAcceleration);
                        ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleDeceleration);
                        //TODO: Handle Throttle Configure
                        break;
                    case ENetCommand.VERIFY_CONNECT:
                        ToHostOrder(ref packet.VerifyConnect.MTU);
                        ToHostOrder(ref packet.VerifyConnect.WindowSize);
                        ToHostOrder(ref packet.VerifyConnect.ChannelCount);
                        ToHostOrder(ref packet.VerifyConnect.IncomingBandwidth);
                        ToHostOrder(ref packet.VerifyConnect.OutgoingBandwidth);
                        ToHostOrder(ref packet.VerifyConnect.PacketThrottleInterval);
                        ToHostOrder(ref packet.VerifyConnect.PacketThrottleAcceleration);
                        ToHostOrder(ref packet.VerifyConnect.PacketThrottleDeceleration);
                        //TODO: Handle Verify Connect
                        break;
                    default:
                        goto finalPacket;
                }
            }
            finalPacket:
            handle.Free();
            #endregion

            if (shuttingDown) shutdownComplete.Set();
        }

        #region "Handler Methods"

        internal Object connectionLock = new Object();

        Nullable<ENetPeer> HandleConnect(IPEndPoint from, ENetProtocolConnect packet)
        {
            lock (connectionLock)
            {
                foreach (var peer in Peers)
                {
                    if (peer.Value.Address == from && peer.Value.SessionID == packet.SessionID) return null;
                }
                ushort peerID;
                if (!AvailablePeerIds.TryDequeue(out peerID)) return null; //No peers available within the client limit
                ENetPeer newPeer = new ENetPeer(from, peerID, packet, ChannelLayout);
                ((IDictionary)Peers).Add(peerID, newPeer);
                return newPeer;
            }
        }

        #endregion

        #region "Utility Methods"

        internal void ToHostOrder(ref ushort data)
        {
            data = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)data));
        }

        internal void ToHostOrder(ref uint data)
        {
            data = (uint)IPAddress.NetworkToHostOrder(unchecked((Int32)data));
        }

        #endregion
    }
}
