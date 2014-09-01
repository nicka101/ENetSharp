using ENetSharp.Container;
using ENetSharp.Internal;
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

        private unsafe void ReceiveDatagram(IAsyncResult ar)
        {
            IPEndPoint fromAddr = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = connection.EndReceive(ar, ref fromAddr);
            if (!shuttingDown) connection.BeginReceive(ReceiveDatagram, null);

            #region "ENet Structure Handling"
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr dataStart = handle.AddrOfPinnedObject();
            ENetProtocolHeader header = (ENetProtocolHeader)Marshal.PtrToStructure(dataStart, typeof(ENetProtocolHeader));
            Util.ToHostOrder(ref header.PeerID);
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
                catch (System.Collections.Generic.KeyNotFoundException)
                {
                    goto finalPacket; //The client doesn't exist and this doesn't follow connection protocol - Ignore them
                }
            }
            int currentDataOffset = ((flag & PROTOCOL_HEADER_FLAG_SENT_TIME) != 0) ? sizeof(ENetProtocolHeader) : sizeof(ENetProtocolHeader) - 2; //sentTime is 2 bytes
            while (currentDataOffset < data.Length)
            {
                ENetProtocol packet = (ENetProtocol)Marshal.PtrToStructure(dataStart + currentDataOffset, typeof(ENetProtocol));
                Util.ToHostOrder(ref packet.Header.ReliableSequenceNumber);
                ENetCommand command = (ENetCommand)(packet.Header.Command & (byte)ENetCommand.COMMAND_MASK);
                if(command >= ENetCommand.COUNT) continue;
                if (peer == null && command != ENetCommand.CONNECT) return; //Peer was following connection protocol but didn't send the connect first
                currentDataOffset += command.Size();
                if (currentDataOffset > data.Length) return; //The ENetCommand is larger than the remaining data
                switch (command)
                {
                    case ENetCommand.ACKNOWLEDGE:
                        Util.ToHostOrder(ref packet.Acknowledge.ReceivedReliableSequenceNumber);
                        Util.ToHostOrder(ref packet.Acknowledge.ReceivedSentTime);
                        //TODO: Handle Acknowledge
                        break;
                    case ENetCommand.BANDWIDTH_LIMIT:
                        Util.ToHostOrder(ref packet.BandwidthLimit.IncomingBandwidth);
                        Util.ToHostOrder(ref packet.BandwidthLimit.OutgoingBandwidth);
                        //TODO: Handle Bandwidth Limit
                        break;
                    case ENetCommand.CONNECT:
                        Console.WriteLine("A connected peer sent a connect packet. WTF are they doing?");
                        Util.ToHostOrder(ref packet.Connect.MTU);
                        Util.ToHostOrder(ref packet.Connect.WindowSize);
                        Util.ToHostOrder(ref packet.Connect.ChannelCount);
                        Util.ToHostOrder(ref packet.Connect.IncomingBandwidth);
                        Util.ToHostOrder(ref packet.Connect.OutgoingBandwidth);
                        Util.ToHostOrder(ref packet.Connect.PacketThrottleInterval);
                        Util.ToHostOrder(ref packet.Connect.PacketThrottleAcceleration);
                        Util.ToHostOrder(ref packet.Connect.PacketThrottleDeceleration);
                        Util.ToHostOrder(ref packet.Connect.SessionID);
                        peer = HandleConnect(fromAddr, packet.Connect);
                        break;
                    case ENetCommand.DISCONNECT:
                        Util.ToHostOrder(ref packet.Disconnect.Data);
                        //TODO: Handle Disconnect
                        break;
                    case ENetCommand.PING:
                        //TODO: Handle Ping
                        break;
                    case ENetCommand.SEND_FRAGMENT:
                        Util.ToHostOrder(ref packet.SendFragment.StartSequenceNumber);
                        Util.ToHostOrder(ref packet.SendFragment.DataLength);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentCount);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentNumber);
                        Util.ToHostOrder(ref packet.SendFragment.TotalLength);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentOffset);
                        currentDataOffset += packet.SendFragment.DataLength;
                        //TODO: Handle Send Fragment
                        break;
                    case ENetCommand.SEND_RELIABLE:
                        Util.ToHostOrder(ref packet.SendReliable.DataLength);
                        //TODO: Handle Send Reliable
                        currentDataOffset += packet.SendReliable.DataLength;
                        break;
                    case ENetCommand.SEND_UNRELIABLE:
                        Util.ToHostOrder(ref packet.SendUnreliable.UnreliableSequenceNumber);
                        Util.ToHostOrder(ref packet.SendUnreliable.DataLength);
                        currentDataOffset += packet.SendUnreliable.DataLength;
                        //TODO: Handle Send Unreliable
                        break;
                    case ENetCommand.SEND_UNSEQUENCED:
                        Util.ToHostOrder(ref packet.SendUnsequenced.UnsequencedGroup);
                        Util.ToHostOrder(ref packet.SendUnsequenced.DataLength);
                        currentDataOffset += packet.SendUnsequenced.DataLength;
                        //TODO: Handle Send Unsequenced
                        break;
                    case ENetCommand.THROTTLE_CONFIGURE:
                        Util.ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleInterval);
                        Util.ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleAcceleration);
                        Util.ToHostOrder(ref packet.ThrottleConfigure.PacketThrottleDeceleration);
                        //TODO: Handle Throttle Configure
                        break;
                    case ENetCommand.VERIFY_CONNECT:
                        Util.ToHostOrder(ref packet.VerifyConnect.MTU);
                        Util.ToHostOrder(ref packet.VerifyConnect.WindowSize);
                        Util.ToHostOrder(ref packet.VerifyConnect.ChannelCount);
                        Util.ToHostOrder(ref packet.VerifyConnect.IncomingBandwidth);
                        Util.ToHostOrder(ref packet.VerifyConnect.OutgoingBandwidth);
                        Util.ToHostOrder(ref packet.VerifyConnect.PacketThrottleInterval);
                        Util.ToHostOrder(ref packet.VerifyConnect.PacketThrottleAcceleration);
                        Util.ToHostOrder(ref packet.VerifyConnect.PacketThrottleDeceleration);
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

        private Nullable<ENetPeer> HandleConnect(IPEndPoint from, ENetProtocolConnect packet)
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

        private unsafe void SendAcks()
        {
            foreach(var peer in Peers.Values){
                peer.SendAcks();
            }
        }

        #endregion
    }
}
