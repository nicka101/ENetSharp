using System;

namespace ENetSharp.Internal.Container
{
    internal class ENetIncomingCommand : IEquatable<ENetIncomingCommand>
    {
        internal readonly ushort SequenceNumber;
        internal uint FragmentsRemaining = 0; //Always 0 if the incoming data was not fragmented
        internal IENetDummy DataHolder;

        internal ENetIncomingCommand(ushort sequenceNumber)
        {
            SequenceNumber = sequenceNumber;
        }

        //TODO: Override equality operators so that it only compares the sequence number
        public bool Equals(ENetIncomingCommand other)
        {
            return SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object other)
        {
            if (other == null) return false;
            var otherMe = other as ENetIncomingCommand;
            if(otherMe == null)return false;
            return SequenceNumber == otherMe.SequenceNumber;
        }

        public override int GetHashCode()
        {
            return SequenceNumber;
        }

        public static bool operator ==(ENetIncomingCommand first, ENetIncomingCommand second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (((object)first == null) || ((object)second == null)) return false;
            return first.SequenceNumber == second.SequenceNumber;
        }

        public static bool operator !=(ENetIncomingCommand first, ENetIncomingCommand second)
        {
            return !(first == second);
        }
    }
}
