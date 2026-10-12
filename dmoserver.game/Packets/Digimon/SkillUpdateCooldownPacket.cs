namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class SkillUpdateCooldownPacket
{
    private uint _digimonHandle;
    private byte _skillSlot;
    private int _cooldownSeconds;

    public SkillUpdateCooldownPacket(uint digimonHandle, byte skillSlot, int cooldownSeconds)
    {
        _digimonHandle = digimonHandle;
        _skillSlot = skillSlot;
        _cooldownSeconds = cooldownSeconds;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(3246);
        writer.WriteInt((int)_digimonHandle);
        writer.WriteInt(1); // Número de skills a actualizar
        
        // Datos de la skill
        writer.WriteInt(_skillSlot);
        
        // DMO calcula el fin del cooldown usando Unix Time (segundos desde 1970)
        int endTime = (int)System.DateTimeOffset.UtcNow.AddSeconds(_cooldownSeconds).ToUnixTimeSeconds();
        writer.WriteInt(endTime);

        return writer.Serialize();
    }
}