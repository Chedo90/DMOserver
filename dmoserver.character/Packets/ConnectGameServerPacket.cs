using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class ConnectGameServerPacket : PacketWriter
{
    private const int PacketNumber = 1703;

    public ConnectGameServerPacket()
    {
        Type(PacketNumber);
    }
}