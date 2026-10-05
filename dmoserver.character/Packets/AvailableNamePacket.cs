using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class AvailableNamePacket : PacketWriter
{
    private const int PacketNumber = 1302;

    public AvailableNamePacket(bool available)
    {
        Type(PacketNumber);
        WriteInt(available ? 1 : 0);
    }
}