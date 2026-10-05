using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class OnConnectEventConnectionPacket : PacketWriter
{
    private const int PacketNumber = 65535;

    public OnConnectEventConnectionPacket(short handshake)
    {
        Type(PacketNumber);
        WriteShort(handshake);
    }
}