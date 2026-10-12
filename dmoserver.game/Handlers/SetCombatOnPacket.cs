namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class SetCombatOnPacket
{
    private readonly uint _handle;

    public SetCombatOnPacket(uint handle)
    {
        _handle = handle;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1034); // opcode sin verificar
        writer.WriteUInt(_handle);
        return writer.Serialize();
    }
}