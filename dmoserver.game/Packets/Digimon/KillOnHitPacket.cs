namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class KillOnHitPacket
{
    public uint AttackerHandle { get; set; }
    public uint TargetHandle { get; set; }
    public int Damage { get; set; }
    public int HitType { get; set; } = 0; // 0 = Normal, 1 = Crítico, 2 = Block

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1020);

        writer.WriteUInt(AttackerHandle);
        writer.WriteUInt(TargetHandle);
        writer.WriteInt(-Damage);
        writer.WriteInt(HitType);
        writer.WriteShort(0);

        return writer.Serialize();
    }
}