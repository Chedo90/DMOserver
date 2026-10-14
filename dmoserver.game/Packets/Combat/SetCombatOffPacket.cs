namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

/// <summary>Saca al objetivo de la postura de combate (opcode 1035, igual que Oldserver).</summary>
public class SetCombatOffPacket
{
    private readonly uint _handle;

    public SetCombatOffPacket(uint handle)
    {
        _handle = handle;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1035);
        writer.WriteUInt(_handle);
        return writer.Serialize();
    }
}
