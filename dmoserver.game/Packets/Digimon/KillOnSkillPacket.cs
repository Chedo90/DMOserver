namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class KillOnSkillPacket
{
    private readonly uint _attacker;
    private readonly uint _target;
    private readonly byte _skillSlot;
    private readonly int _damage;

    public KillOnSkillPacket(uint attacker, uint target, byte skillSlot, int damage)
    {
        _attacker = attacker;
        _target = target;
        _skillSlot = skillSlot;
        _damage = damage;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1021);

        writer.WriteInt((int)_attacker);
        writer.WriteInt((int)_target);
        writer.WriteInt(_skillSlot);
        writer.WriteInt(_damage * -1);

        return writer.Serialize();
    }
}