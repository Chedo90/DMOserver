namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class SkillHitPacket
{
    private readonly uint _attacker;
    private readonly uint _target;
    private readonly byte _skillSlot;
    private readonly int _damage;
    private readonly byte _targetHpRate;

    public SkillHitPacket(uint attacker, uint target, byte skillSlot, int damage, byte targetHpRate)
    {
        _attacker = attacker;
        _target = target;
        _skillSlot = skillSlot;
        _damage = damage;
        _targetHpRate = targetHpRate;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1102);

        writer.WriteByte(0);                    // byte inicial, necesario
        writer.WriteInt((int)_attacker);
        writer.WriteInt((int)_target);
        writer.WriteInt(_skillSlot);
        writer.WriteByte(255);
        writer.WriteByte(_targetHpRate);
        writer.WriteByte(18);
        writer.WriteByte(4);
        writer.WriteInt(_damage * -1);
        writer.WriteByte(255);
        writer.WriteByte(0);

        return writer.Serialize();
    }
}