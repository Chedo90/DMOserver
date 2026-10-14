namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class CastSkillPacket
{
    private readonly byte _skillSlot;
    private readonly uint _attacker;
    private readonly uint _target;

    public CastSkillPacket(byte skillSlot, uint attacker, uint target)
    {
        _skillSlot = skillSlot;
        _attacker = attacker;
        _target = target;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1015);

        writer.WriteByte(_skillSlot);
        writer.WriteShort((short)_attacker);   // 2 bytes (igual que Oldserver)
        writer.WriteShort((short)_target);     // 2 bytes (igual que Oldserver)

        return writer.Serialize();
    }
}