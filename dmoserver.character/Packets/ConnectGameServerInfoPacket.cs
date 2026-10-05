using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class ConnectGameServerInfoPacket : PacketWriter
{
    private const int PacketNumber = 1308;

    public ConnectGameServerInfoPacket(string ipAddress, string port, short mapId)
    {
        Type(PacketNumber);

        WriteString(ipAddress);
        WriteInt(int.Parse(port));
        WriteInt(mapId);
    }
}