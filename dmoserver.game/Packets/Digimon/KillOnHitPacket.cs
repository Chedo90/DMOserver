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
        using var writer = new PacketWriter(1020); // Opcode 1020 = Muerte por golpe

        writer.WriteUInt(AttackerHandle);
        writer.WriteUInt(TargetHandle);
        writer.WriteInt(-Damage); // DMO requiere que el daño de muerte vaya en negativo
        writer.WriteInt(HitType);
        writer.WriteShort(0);     // Los 2 bytes de relleno del final

        return writer.Serialize();
    }
}