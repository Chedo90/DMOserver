namespace dmoserver.game.Packets.Digimon;

using dmoserver.game.Network;

/// <summary>
/// Paquete 1105: Sincroniza en tiempo real los puntos de habilidad (Skill Points), la maestría (Mastery)
/// y la barra de experiencia de habilidad (Skill Experience) en la interfaz del cliente DMO.
/// </summary>
public sealed class UpdateSkillProgressPacket
{
    public const short OpCode = 1105;

    public uint PartnerHandle { get; set; }
    public byte FormSlot { get; set; }
    public byte SkillPoints { get; set; }
    public byte SkillMastery { get; set; }
    public int SkillExperience { get; set; }

    public UpdateSkillProgressPacket(uint partnerHandle, byte formSlot, byte skillPoints, byte skillMastery, int skillExperience)
    {
        PartnerHandle = partnerHandle;
        FormSlot = formSlot;
        SkillPoints = skillPoints;
        SkillMastery = skillMastery;
        SkillExperience = skillExperience;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(OpCode);
        writer.WriteUInt(PartnerHandle);
        writer.WriteByte(FormSlot);
        writer.WriteByte(SkillPoints);
        writer.WriteByte(SkillMastery);
        writer.WriteInt(SkillExperience);
        return writer.Serialize();
    }
}
