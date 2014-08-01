using ENetSharp.Internal.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Structures
{
    internal enum ENetCommand
    {
        ENET_PROTOCOL_COMMAND_NONE = 0,
        ENET_PROTOCOL_COMMAND_ACKNOWLEDGE = 1,
        ENET_PROTOCOL_COMMAND_CONNECT = 2,
        ENET_PROTOCOL_COMMAND_VERIFY_CONNECT = 3,
        ENET_PROTOCOL_COMMAND_DISCONNECT = 4,
        ENET_PROTOCOL_COMMAND_PING = 5,
        ENET_PROTOCOL_COMMAND_SEND_RELIABLE = 6,
        ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE = 7,
        ENET_PROTOCOL_COMMAND_SEND_FRAGMENT = 8,
        ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED = 9,
        ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT = 10,
        ENET_PROTOCOL_COMMAND_THROTTLE_CONFIGURE = 11,
        ENET_PROTOCOL_COMMAND_COUNT = 12,

        ENET_PROTOCOL_COMMAND_MASK = 0x0F
    }

    internal static class ENetCommandExtensions
    {
        //Must be marked unsafe as the JIT is usually in charge of organizing struct layout
        public static unsafe int Size(this ENetCommand command)
        {
            switch (command)
            {
                case ENetCommand.ENET_PROTOCOL_COMMAND_ACKNOWLEDGE:
                    return sizeof(ENetProtocolAcknowledge);
                case ENetCommand.ENET_PROTOCOL_COMMAND_CONNECT:
                    return sizeof(ENetProtocolConnect);
                case ENetCommand.ENET_PROTOCOL_COMMAND_VERIFY_CONNECT:
                    return sizeof(ENetProtocolVerifyConnect);
                case ENetCommand.ENET_PROTOCOL_COMMAND_DISCONNECT:
                    return sizeof(ENetProtocolDisconnect);
                case ENetCommand.ENET_PROTOCOL_COMMAND_PING:
                    return sizeof(ENetProtocolPing);
                case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
                    return sizeof(ENetProtocolSendReliable);
                case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
                    return sizeof(ENetProtocolSendUnreliable);
                case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
                    return sizeof(ENetProtocolSendFragment);
                case ENetCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
                    return sizeof(ENetProtocolSendUnsequenced);
                case ENetCommand.ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT:
                    return sizeof(ENetProtocolBandwidthLimit);
                case ENetCommand.ENET_PROTOCOL_COMMAND_THROTTLE_CONFIGURE:
                    return sizeof(ENetProtocolThrottleConfigure);
                default:
                    return 0;
            }
        }
    }
}
