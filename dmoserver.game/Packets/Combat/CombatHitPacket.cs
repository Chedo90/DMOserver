namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class CombatHitPacket
{
    public uint AttackerHandle { get; set; }
    public uint TargetHandle { get; set; }
    public int Damage { get; set; }
    public int HitType { get; set; } = 0;
    public int HpAfterHit { get; set; }
    public int HpBeforeHit { get; set; }
    public int Opcode { get; set; } = 1013;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);
        writer.WriteUInt(AttackerHandle);
        writer.WriteUInt(TargetHandle);
        writer.WriteInt(-Damage);
        writer.WriteInt(HitType);
        writer.WriteInt(HpAfterHit);
        writer.WriteInt(HpBeforeHit);
        writer.WriteShort(0);
        return writer.Serialize();
    }
}