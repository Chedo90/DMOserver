namespace dmoserver.game.Packets;

using dmoserver.game.Network;

// Constructor Primario C# 12+
public sealed class ConnectionPacket(short handshake, uint timestamp)
{
    public const short OpCode = -2;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(OpCode);
        writer.WriteShort(handshake);
        writer.WriteUInt(timestamp);
        return writer.Serialize();
    }
}