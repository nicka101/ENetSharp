using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Internal
{
    internal static class Util
    {
        #region Endianness

        internal static void ToHostOrder(ref ushort data)
        {
            data = (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)data));
        }

        internal static void ToHostOrder(ref uint data)
        {
            data = (uint)IPAddress.NetworkToHostOrder(unchecked((Int32)data));
        }

        internal static void ToNetOrder(ref ushort data)
        {
            data = (ushort)IPAddress.HostToNetworkOrder(unchecked((Int16)data));
        }

        internal static void ToNetOrder(ref uint data)
        {
            data = (uint)IPAddress.HostToNetworkOrder(unchecked((Int32)data));
        }

        internal static ushort ToHostOrder(ushort data)
        {
            return (ushort)IPAddress.NetworkToHostOrder(unchecked((Int16)data));
        }

        internal static uint ToHostOrder(uint data)
        {
            return (uint)IPAddress.NetworkToHostOrder(unchecked((Int32)data));
        }

        internal static ushort ToNetOrder(ushort data)
        {
            return (ushort)IPAddress.HostToNetworkOrder(unchecked((Int16)data));
        }

        internal static uint ToNetOrder(uint data)
        {
            return (uint)IPAddress.HostToNetworkOrder(unchecked((Int32)data));
        }

        #endregion

        internal static ushort Timestamp()
        {
            return (ushort)Environment.TickCount;
        }
    }
}
