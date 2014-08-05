using ENetSharp.Internal.Structures;
using ENetSharp.Structures;
using System;
using System.Collections.Generic;
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
        internal const int PROTOCOL_MAXIMUM_PEER_ID = 0x007F;

        private UdpClient connection;
        private bool shuttingDown = false;
        private ManualResetEventSlim shutdownComplete = new ManualResetEventSlim(false);
        private readonly int PeerCount;
        private Dictionary<ushort, ENetPeer> Peers;

        public delegate void ConnectHandler(ENetPeer peer);
        public delegate void DisconnectHandler(ENetPeer peer);
        public delegate void DataHandler(ENetPeer peer, byte[] data);

        public event ConnectHandler OnConnect;
        public event DisconnectHandler OnDisconnect;
        public event DataHandler OnData;

        public ENetHost(IPEndPoint listenAddress)
        {
            connection = new UdpClient(listenAddress);
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

            #region "ENet Parsing"
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
                        peer.Value.Address != fromAddr /* && peer.Value.Address != ENET_HOST_BROADCAST */) //Figure out why ENET_HOST_BROADCAST is needed
                    {
                        return; //The peer is disconnected, dead or the packets origin doesn't match the peer - Ignore them
                    }
                }
                catch (KeyNotFoundException)
                {
                    return; //Ignore the data as the client doesn't exist and this doesn't follow connection protocol - Ignore them
                }
            }
            int currentDataOffset = ((flag & 0x80) != 0) ? sizeof(ENetProtocolHeader) : 2; //sentTime is 2 bytes from the start of the header
            while (currentDataOffset < data.Length)
            {
                ENetProtocol packet = (ENetProtocol)Marshal.PtrToStructure(dataStart + currentDataOffset, typeof(ENetProtocol));
                packet.Header.ReliableSequenceNumber = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)packet.Header.ReliableSequenceNumber));
                if (peer == null && (ENetCommand)(packet.Header.Command & (byte)ENetCommand.ENET_PROTOCOL_COMMAND_MASK) != ENetCommand.ENET_PROTOCOL_COMMAND_CONNECT) return; //Peer was following connection protocol but didn't send the connect first
                switch ((ENetCommand)(packet.Header.Command & (byte)ENetCommand.ENET_PROTOCOL_COMMAND_MASK))
                {
                    case ENetCommand.ENET_PROTOCOL_COMMAND_ACKNOWLEDGE:
                        //TODO: Handle Acknowledge
                        currentDataOffset += sizeof(ENetProtocolAcknowledge);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT:
                        //TODO: Handle Bandwidth Limit
                        currentDataOffset += sizeof(ENetProtocolBandwidthLimit);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_CONNECT:
                        //TODO: Handle Connect
                        currentDataOffset += sizeof(ENetProtocolConnect);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_DISCONNECT:
                        //TODO: Handle Disconnect
                        currentDataOffset += sizeof(ENetProtocolDisconnect);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_PING:
                        //TODO: Handle Ping
                        currentDataOffset += sizeof(ENetProtocolPing);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
                        //TODO: Handle Send Fragment
                        packet.SendFragment.DataLength = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)packet.SendFragment.DataLength));
                        currentDataOffset += sizeof(ENetProtocolSendFragment);
                        currentDataOffset += packet.SendFragment.DataLength;
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
                        //TODO: Handle Send Reliable
                        packet.SendReliable.DataLength = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)packet.SendReliable.DataLength));
                        currentDataOffset += sizeof(ENetProtocolSendReliable);
                        currentDataOffset += packet.SendReliable.DataLength;
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
                        //TODO: Handle Send Unreliable
                        packet.SendUnreliable.DataLength = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)packet.SendUnreliable.DataLength));
                        currentDataOffset += sizeof(ENetProtocolSendUnreliable);
                        currentDataOffset += packet.SendUnreliable.DataLength;
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
                        //TODO: Handle Send Unsequenced
                        packet.SendUnsequenced.DataLength = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)packet.SendUnsequenced.DataLength));
                        currentDataOffset += sizeof(ENetProtocolSendUnsequenced);
                        currentDataOffset += packet.SendUnsequenced.DataLength;
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_THROTTLE_CONFIGURE:
                        //TODO: Handle Throttle Configure
                        currentDataOffset += sizeof(ENetProtocolThrottleConfigure);
                        break;
                    case ENetCommand.ENET_PROTOCOL_COMMAND_VERIFY_CONNECT:
                        //TODO: Handle Verify Connect
                        currentDataOffset += sizeof(ENetProtocolVerifyConnect);
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
    }
}
