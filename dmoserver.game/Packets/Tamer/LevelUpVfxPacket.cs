using dmoserver.game.Network;

namespace dmoserver.game.Packets;

public class LevelUpPacket
{
    public uint Handle { get; set; }
    public byte Level { get; set; }
    public short Opcode => 1019; // El OpCode exacto de tu Old Server

    public LevelUpPacket(uint handle, byte level)
    {
        Handle = handle;
        Level = level;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);
        writer.WriteUInt(Handle);
        writer.WriteByte(Level); // Solo envía el nuevo nivel
        return writer.Serialize();
    }
}