using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetSharp.Protocol
{
    internal enum ENetSendType
    {
        UNRELIABLE = 0,
        UNSEQUENCED = 64,
        RELIABLE = 128
    }
}
