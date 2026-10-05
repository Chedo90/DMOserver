namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public sealed class TamerWalkPacket(int x, int y, uint handle = 100000)
{
    private const int PacketNumber = 1006;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteByte(5);       // Acción: Desplazamiento
        writer.WriteShort(1);
        writer.WriteUInt(handle);   // Handle del Tamer
        writer.WriteInt(x);
        writer.WriteInt(y);
        writer.WriteInt(0);

        return writer.Serialize();
    }
}