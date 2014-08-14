using ENetSharp.Internal.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal.Protocol
{
    internal enum ENetCommand
    {
        NONE = 0,
        ACKNOWLEDGE = 1,
        CONNECT = 2,
        VERIFY_CONNECT = 3,
        DISCONNECT = 4,
        PING = 5,
        SEND_RELIABLE = 6,
        SEND_UNRELIABLE = 7,
        SEND_FRAGMENT = 8,
        SEND_UNSEQUENCED = 9,
        BANDWIDTH_LIMIT = 10,
        THROTTLE_CONFIGURE = 11,
        COUNT = 12,

        ENET_PROTOCOL_COMMAND_MASK = 0x0F
    }

    internal static class ENetCommandExtensions
    {
        //Must be marked unsafe as the JIT is usually in charge of organizing struct layout
        public static unsafe int Size(this ENetCommand command)
        {
            switch (command)
            {
                case ENetCommand.ACKNOWLEDGE:
                    return sizeof(ENetProtocolAcknowledge);
                case ENetCommand.CONNECT:
                    return sizeof(ENetProtocolConnect);
                case ENetCommand.VERIFY_CONNECT:
                    return sizeof(ENetProtocolVerifyConnect);
                case ENetCommand.DISCONNECT:
                    return sizeof(ENetProtocolDisconnect);
                case ENetCommand.PING:
                    return sizeof(ENetProtocolPing);
                case ENetCommand.SEND_RELIABLE:
                    return sizeof(ENetProtocolSendReliable);
                case ENetCommand.SEND_UNRELIABLE:
                    return sizeof(ENetProtocolSendUnreliable);
                case ENetCommand.SEND_FRAGMENT:
                    return sizeof(ENetProtocolSendFragment);
                case ENetCommand.SEND_UNSEQUENCED:
                    return sizeof(ENetProtocolSendUnsequenced);
                case ENetCommand.BANDWIDTH_LIMIT:
                    return sizeof(ENetProtocolBandwidthLimit);
                case ENetCommand.THROTTLE_CONFIGURE:
                    return sizeof(ENetProtocolThrottleConfigure);
                default:
                    return 0;
            }
        }
    }
}
