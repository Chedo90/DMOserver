namespace dmoserver.game.Packets.Combat;

using System;
using dmoserver.game.Network;

public class SkillUpdateCooldownPacket
{
    private readonly uint _digimonHandle;
    private readonly int _currentType;
    private readonly int _skillId;
    private readonly int _cooldownSeconds;

    public SkillUpdateCooldownPacket(uint digimonHandle, int currentType, int skillId, int cooldownSeconds)
    {
        _digimonHandle = digimonHandle;
        _currentType = currentType;
        _skillId = skillId;
        _cooldownSeconds = cooldownSeconds;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(3246);
        writer.WriteInt((int)_digimonHandle);
        writer.WriteInt(_currentType);
        writer.WriteInt(1); // Número de skills a actualizar
        
        // ID oficial de la habilidad (ej: 3100111)
        writer.WriteInt(_skillId);
        
        // DMO calcula el fin del cooldown usando Unix Time (segundos desde 1970)
        int endTime = (int)DateTimeOffset.UtcNow.AddSeconds(_cooldownSeconds).ToUnixTimeSeconds();
        writer.WriteInt(endTime);

        return writer.Serialize();
    }
}