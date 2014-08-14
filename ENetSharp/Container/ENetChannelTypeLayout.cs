using ENetSharp.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Container
{
    public class ENetChannelTypeLayout
    {
        internal ENetSendType[] channels;
        public ENetChannelTypeLayout(params ENetSendType[] channels)
        {
            if (channels.Length > ENetHost.PROTOCOL_MAXIMUM_CHANNEL_COUNT) throw new ArgumentException("You specified too many channels!");
            else if (channels.Length < ENetHost.PROTOCOL_MINIMUM_CHANNEL_COUNT) throw new ArgumentException("You specified too few channels!");
            this.channels = channels;
        }

        internal ENetSendType this[byte idx]
        {
            get
            {
                return channels[idx];
            }
        }

        internal byte ChannelCount()
        {
            return (byte)channels.Length;
        }
    }
}
