using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Structures
{
    public enum ENetPeerState
    {
        DISCONNECTED = 0,
        CONNECTING = 1,
        ACKNOWLEDGING_CONNECT = 2,
        CONNECTION_PENDING = 3,
        CONNECTION_SUCCEEDED = 4,
        CONNECTED = 5,
        DISCONNECT_LATER = 6,
        DISCONNECTING = 7,
        ACKNOWLEDGING_DISCONNECT = 8,
        ZOMBIE = 9
    }
}
