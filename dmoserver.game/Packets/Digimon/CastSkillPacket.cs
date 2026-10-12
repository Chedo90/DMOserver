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
        writer.WriteUInt(_attacker);   // 4 bytes
        writer.WriteUInt(_target);     // 4 bytes

        return writer.Serialize();
    }
}