using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class ConnectionPacket : PacketWriter
{
    private const int PacketNumber = -2;

    public ConnectionPacket(short handshake, uint handshakeTimestamp)
    {
        Type(PacketNumber);
        WriteShort(handshake);
        WriteUInt(handshakeTimestamp);
    }
}