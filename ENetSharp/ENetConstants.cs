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
    class ENetConstants {
        #region Protocol Constants
        internal const byte PROTOCOL_MINIMUM_CHANNEL_COUNT = 1;
        internal const byte PROTOCOL_MAXIMUM_CHANNEL_COUNT = 255;
        internal const int PROTOCOL_MAXIMUM_PEER_ID = 0x007F;
        internal const ushort PEER_RELIABLE_WINDOW_SIZE = 0x1000;
        internal const ushort PEER_RELIABLE_WINDOWS = 16;
        internal const ushort PEER_FREE_RELIABLE_WINDOWS = 8;
        #endregion
    }
}
