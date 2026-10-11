namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class MobStatePacket
{
    private readonly uint _handle;

    public MobStatePacket(uint handle)
    {
        _handle = handle;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1070); // Opcode 0x042E

        writer.WriteUInt(_handle);
        writer.WriteInt(0); // Estado 0 = Idle

        // Delimitador de verificación de 1070
        writer.WriteByte(0x32);
        writer.WriteByte(0x1A);

        return writer.Serialize();
    }
}