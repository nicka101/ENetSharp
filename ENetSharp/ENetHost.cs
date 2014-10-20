using ENetSharp.Container;
using ENetSharp.Internal;
using ENetSharp.Internal.Container;
using ENetSharp.Internal.Protocol;
using ENetSharp.Protocol;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace ENetSharp
{
    public class ENetHost : IDisposable
    {
        #region Protocol Header
        internal const ushort PROTOCOL_HEADER_FLAG_SENT_TIME = 1 << 15;
        //internal const ushort PROTOCOL_HEADER_FLAG_COMPRESSED = 1 << 14;
        internal const ushort PROTOCOL_HEADER_FLAG_MASK = PROTOCOL_HEADER_FLAG_SENT_TIME; //| PROTOCOL_HEADER_FLAG_COMPRESSED;

        internal const ushort PROTOCOL_HEADER_SESSION_SHIFT = 12;
        internal const ushort PROTOCOL_HEADER_SESSION_MASK = 3 << PROTOCOL_HEADER_SESSION_SHIFT;
        #endregion

        #region Protocol Command Header
        internal const byte PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE = 1 << 7;
        internal const byte PROTOCOL_COMMAND_FLAG_UNSEQUENCED = 1 << 6;

        internal const byte PROTOCOL_COMMAND_ID_MASK = 0xf;
        #endregion

        #region Protocol Constants
        internal const byte PROTOCOL_MINIMUM_CHANNEL_COUNT = 1;
        internal const byte PROTOCOL_MAXIMUM_CHANNEL_COUNT = 255;
        internal const int PROTOCOL_MAXIMUM_PEER_ID = 0x007F;
        internal const ushort PEER_RELIABLE_WINDOW_SIZE = 0x1000;
        internal const ushort PEER_RELIABLE_WINDOWS = 16;
        internal const ushort PEER_FREE_RELIABLE_WINDOWS = 8;
        #endregion

        private UdpClient connection;
        private bool shuttingDown = false;
        private ManualResetEventSlim shutdownComplete = new ManualResetEventSlim(false);
        
        private readonly ushort PeerCount;
        private readonly ConcurrentDictionary<ushort, ENetPeer> Peers = new ConcurrentDictionary<ushort, ENetPeer>();
        private readonly ConcurrentQueue<ushort> AvailablePeerIds = new ConcurrentQueue<ushort>();
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

            // Queue the next datagram
            if (!shuttingDown) connection.BeginReceive(ReceiveDatagram, null);

            #region "ENet Structure Handling"
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr dataStart = handle.AddrOfPinnedObject();
            ENetProtocolHeader header = (ENetProtocolHeader)Marshal.PtrToStructure(dataStart, typeof(ENetProtocolHeader));
            Util.ToHostOrder(ref header.PeerID);

            ushort flags = (ushort) (header.PeerID & PROTOCOL_HEADER_FLAG_MASK);
            byte sessionId = (byte) ((header.PeerID & PROTOCOL_HEADER_SESSION_MASK) >> PROTOCOL_HEADER_SESSION_SHIFT);
            header.PeerID &= unchecked((ushort) ~(PROTOCOL_HEADER_FLAG_MASK | PROTOCOL_HEADER_SESSION_MASK));

            ENetPeer? peer = null;
            if (header.PeerID != PROTOCOL_MAXIMUM_PEER_ID) //peer remains null if the first command is expected to be a connect
            {
                try
                {
                    peer = Peers[header.PeerID];
                    if (peer.Value.State == ENetPeerState.Disconnected || 
                        peer.Value.State == ENetPeerState.Zombie ||
                        //Don't include ENET_HOST_BROADCAST, it's meant for clients broadcasting the connect packet and communicating with any server that responds
                        !peer.Value.Address.Equals(fromAddr) /* && peer.Value.Address != ENET_HOST_BROADCAST */)
                    {
                        goto finalPacket; //The peer is disconnected, dead or the packets origin doesn't match the peer - Ignore them
                    }
                }
                catch (System.Collections.Generic.KeyNotFoundException)
                {
                    goto finalPacket; //The client doesn't exist and this doesn't follow connection protocol - Ignore them
                }
            }

            int currentDataOffset = ((flags & PROTOCOL_HEADER_FLAG_SENT_TIME) != 0) ? sizeof(ENetProtocolHeader) : sizeof(ENetProtocolHeader) - 2; //sentTime is 2 bytes
            
            while (currentDataOffset < data.Length)
            {
                ENetProtocol packet = (ENetProtocol)Marshal.PtrToStructure(dataStart + currentDataOffset, typeof(ENetProtocol));
                Util.ToHostOrder(ref packet.Header.ReliableSequenceNumber);


                ENetCommand command = (ENetCommand)(packet.Header.Command & (byte)ENetCommand.COMMAND_MASK); // TODO: ACKNOWLEDGE and UNSEQUENCED flag handling
                currentDataOffset += command.Size();

                if (packet.Header.ChannelID >= ChannelLayout.ChannelCount()) continue; // Skip invalid command

                if (command >= ENetCommand.COUNT) return;                   // Nonexistant or not-implemented commands
                if (peer == null && command != ENetCommand.CONNECT) return; //Peer was following connection protocol but didn't send the connect first
                if (currentDataOffset > data.Length) return;                //The ENetCommand is larger than the remaining data
                
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
                        //Ping has no handling in the real ENet
                        break;
                    case ENetCommand.SEND_FRAGMENT:
                        Util.ToHostOrder(ref packet.SendFragment.DataLength); //We have to assume the fragment is the right type for the channel until I figure out how it indicates it
                        Util.ToHostOrder(ref packet.SendFragment.StartSequenceNumber);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentCount);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentNumber);
                        Util.ToHostOrder(ref packet.SendFragment.TotalLength);
                        Util.ToHostOrder(ref packet.SendFragment.FragmentOffset);
                        byte[] fragmentData = new byte[packet.SendFragment.DataLength];
                        Marshal.Copy(dataStart + currentDataOffset, fragmentData, 0, packet.SendFragment.DataLength);
                        currentDataOffset += packet.SendFragment.DataLength;
                        HandleReliable(peer.Value, packet, true, fragmentData);
                        break;
                    case ENetCommand.SEND_RELIABLE:
                        Util.ToHostOrder(ref packet.SendReliable.DataLength);
                        if ((ChannelLayout[packet.Header.ChannelID] & ENetSendType.RELIABLE) == 0) //Each of the data handlers uses this to quickly discard any data not matching the channels send type
                        {
                            currentDataOffset += packet.SendReliable.DataLength;
                            break;
                        }
                        byte[] reliableData = new byte[packet.SendReliable.DataLength];
                        Marshal.Copy(dataStart + currentDataOffset, reliableData, 0, packet.SendReliable.DataLength);
                        currentDataOffset += packet.SendReliable.DataLength;
                        HandleReliable(peer.Value, packet, false, reliableData);
                        break;
                    case ENetCommand.SEND_UNRELIABLE:
                        Util.ToHostOrder(ref packet.SendUnreliable.DataLength);
                        if ((ChannelLayout[packet.Header.ChannelID] & ENetSendType.UNRELIABLE) == 0)
                        {
                            currentDataOffset += packet.SendUnreliable.DataLength;
                            break;
                        }
                        Util.ToHostOrder(ref packet.SendUnreliable.UnreliableSequenceNumber);
                        byte[] unreliableData = new byte[packet.SendUnreliable.DataLength];
                        Marshal.Copy(dataStart + currentDataOffset, unreliableData, 0, packet.SendUnreliable.DataLength);
                        currentDataOffset += packet.SendUnreliable.DataLength;
                        HandleUnreliable(peer.Value, packet.SendUnreliable, unreliableData);
                        break;
                    case ENetCommand.SEND_UNSEQUENCED:
                        Util.ToHostOrder(ref packet.SendUnsequenced.DataLength);
                        if ((ChannelLayout[packet.Header.ChannelID] & ENetSendType.UNSEQUENCED) == 0)
                        {
                            currentDataOffset += packet.SendUnsequenced.DataLength;
                            break;
                        }
                        Util.ToHostOrder(ref packet.SendUnsequenced.UnsequencedGroup);
                        byte[] unsequencedData = new byte[packet.SendUnsequenced.DataLength];
                        Marshal.Copy(dataStart + currentDataOffset, unsequencedData, 0, packet.SendUnsequenced.DataLength);
                        currentDataOffset += packet.SendUnsequenced.DataLength;
                        HandleUnsequenced(peer.Value, packet.SendUnsequenced, unsequencedData);
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

        private ENetPeer? HandleConnect(IPEndPoint from, ENetProtocolConnect packet)
        {
            lock (connectionLock)
            {
                foreach (var peer in Peers)
                {
                    if (peer.Value.Address.Equals(from) && peer.Value.SessionID == packet.SessionID) return null;
                }
                ushort peerId;
                if (!AvailablePeerIds.TryDequeue(out peerId)) return null; //No peers available within the client limit
                ENetPeer newPeer = new ENetPeer(from, peerId, packet, ChannelLayout);
                ((IDictionary)Peers).Add(peerId, newPeer);
                return newPeer;
            }
        }

        private void HandleReliable(ENetPeer peer, ENetProtocol packet, bool isFragment, byte[] data)
        {
            if (peer.State != ENetPeerState.Connected && peer.State != ENetPeerState.DisconnectLater) return;
            var channel = peer.Channels[packet.Header.ChannelID];
            if (DropSequencedData(channel, packet.Header) || packet.Header.ReliableSequenceNumber == channel.IncomingSequenceNumber) return;
            if (!isFragment && packet.Header.ReliableSequenceNumber == (channel.IncomingSequenceNumber + 1))
            {
                if (OnData != null) OnData(peer, data);
                channel.IncomingSequenceNumber++;
                return;
            }
            lock (channel.IncomingCommandLock)
            {
                var command = new ENetIncomingCommand(packet.Header.ReliableSequenceNumber);
                var existingCommand = channel.IncomingCommands.Find(command); //Establish if we already have the packet
                if (existingCommand != null && !isFragment) return; //We already have the command, ignore it
                else if (existingCommand != null)
                {
                    if (existingCommand.Value.FragmentsRemaining == 0) return;
                }
                if (isFragment)
                {
                    command.FragmentsRemaining = packet.SendFragment.FragmentCount - 1;
                }
                if (packet.Header.ReliableSequenceNumber == (channel.IncomingSequenceNumber + 1)) channel.IncomingCommands.AddFirst();
            }
        }

        private void HandleUnreliable(ENetPeer peer, ENetProtocolSendUnreliable packet, byte[] data)
        {
            if (peer.State != ENetPeerState.Connected && peer.State != ENetPeerState.DisconnectLater) return;
            var channel = peer.Channels[packet.Header.ChannelID];
            if (DropSequencedData(channel, packet.Header) || packet.UnreliableSequenceNumber <= channel.IncomingSequenceNumber) return;

        }

        bool DropSequencedData(ENetChannel channel, ENetProtocolCommandHeader header)
        {
            ushort reliableWindow = (ushort)(header.ReliableSequenceNumber / PEER_RELIABLE_WINDOW_SIZE);
            ushort currentWindow = (ushort)(channel.IncomingSequenceNumber / PEER_RELIABLE_WINDOW_SIZE);
            if (header.ReliableSequenceNumber < channel.IncomingSequenceNumber) reliableWindow += PEER_RELIABLE_WINDOWS;
            return reliableWindow < currentWindow || reliableWindow >= currentWindow + PEER_FREE_RELIABLE_WINDOWS - 1;
        }

        private void HandleUnsequenced(ENetPeer peer, ENetProtocolSendUnsequenced packet, byte[] data)
        {

        }

        private void SendAcks()
        {
            foreach(var peer in Peers.Values) peer.SendAcks();
        }

        #endregion
    }
}
