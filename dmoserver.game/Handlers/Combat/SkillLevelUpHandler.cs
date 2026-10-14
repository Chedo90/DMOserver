namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets.Digimon;

/// <summary>
/// Opcode 1104: Maneja la petición del cliente al hacer clic en el botón '+' de una habilidad (F1, F2).
/// Valida puntos de habilidad disponibles, sube el nivel de la skill, guarda en base de datos
/// y responde con el paquete 1105 para sincronizar la interfaz.
/// </summary>
public sealed class SkillLevelUpHandler : IPacketHandler
{
    public const short OpCode = 1104;

    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        int generalHandler = packet.ReadInt();
        byte formSlot = packet.ReadByte();  // 1 = Rookie, 2 = Champion, etc.
        byte skillSlot = packet.ReadByte(); // 0 = F1, 1 = F2, etc.

        var player = client.Player;
        if (player == null || player.Character?.Partner == null) return;
        var partner = player.Character.Partner;

        int slot = skillSlot + 1; // 1 = F1, 2 = F2

        // En DMO oficial, subir un nivel de habilidad requiere 2 Skill Points
        const byte requiredPoints = 2;

        if (partner.SkillPoints < requiredPoints)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[SkillUp] Puntos insuficientes ({partner.SkillPoints}/{requiredPoints}) para subir F{slot}.");
            Console.ResetColor();
            return;
        }

        byte currentLevel = player.GetSkillLevel(slot);
        const byte maxLevel = 10;
        if (currentLevel >= maxLevel)
        {
            Console.WriteLine($"[SkillUp] Habilidad F{slot} ya está al nivel máximo ({maxLevel}).");
            return;
        }

        // Aplicar la subida de nivel y consumir los puntos
        partner.SkillPoints -= requiredPoints;
        byte newLevel = (byte)(currentLevel + 1);
        player.SetSkillLevel(slot, newLevel);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[⬆️ Skill Up] Partner {player.PartnerHandle:X} subió habilidad F{slot} a Nivel {newLevel}! Puntos restantes: {partner.SkillPoints}");
        Console.ResetColor();

        // Persistir en MongoDB
        if (player.Account != null && player.Character != null)
        {
            await GameClient.Db.UpdatePartnerSkillsAsync(
                player.Account.AccountId,
                player.Character.Slot,
                partner.SkillPoints,
                partner.SkillMastery,
                partner.SkillExperience,
                player.GetSkillLevel(1),
                player.GetSkillLevel(2),
                player.GetSkillLevel(3),
                player.GetSkillLevel(4),
                player.GetSkillLevel(5)
            );
        }

        // Sincronizar con el cliente mediante el paquete oficial 1105
        var updatePacket = new UpdateSkillProgressPacket(
            player.PartnerHandle,
            formSlot,
            partner.SkillPoints,
            partner.SkillMastery,
            partner.SkillExperience
        ).Serialize();

        await client.SendAsync(updatePacket);
    }
}