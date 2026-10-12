namespace dmoserver.game.Handlers;

using dmoserver.game.Entities;
using dmoserver.game.Entities.Stats;
using dmoserver.game.Network;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Combat;
using System;
using System.Threading.Tasks;

public static class CombatHandler
{
    // Ponlo en false si el cliente muestra "unknown protocol(1034)"
    private const bool UseCombatOnPackets = true;

    // ==========================================
    // 1. ATAQUE BÁSICO
    // ==========================================
    public static async Task HandleAttackRequestAsync(GameClient client, GamePacketReader packet)
    {
        uint attackerHandle = packet.ReadUInt();
        uint targetHandle = packet.ReadUInt();
        short animationExtra = packet.ReadShort();

        Console.WriteLine($"[DEBUG] Petición 1013 recibida | Atacante: {attackerHandle:X} | Target: {targetHandle:X} | Mapa Asignado: {client.CurrentMap != null}");

        var targetMonster = client.CurrentMap?.GetMonster(targetHandle);

        if (targetMonster == null)
        {
            Console.WriteLine($"[DEBUG] ERROR: El monstruo con Handle {targetHandle:X} no existe en la memoria del mapa.");
            return;
        }

        if (targetMonster.IsDead)
        {
            Console.WriteLine($"[DEBUG] Ignorado: El monstruo {targetHandle:X} ya está marcado como muerto.");
            return;
        }

        int attackerAtk = client.Player?.PartnerStats.Attack ?? 100;
        int targetDef = targetMonster.Stats.Defense;

        int baseDamage = Math.Max(1, attackerAtk - targetDef);
        int variation = Random.Shared.Next(-5, 6);
        int damage = Math.Max(1, baseDamage + variation);

        bool isKilled = targetMonster.TakeDamage(damage);

        Console.ForegroundColor = isKilled ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.WriteLine($"[⚔️ Combate] {attackerHandle:X} ataca a {targetHandle:X} por {damage} (AT: {attackerAtk} vs DE: {targetDef}). Muerto: {isKilled}");
        Console.ResetColor();

        if (isKilled)
        {
            var killPacket = new KillOnHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = 0
            };

            client.CurrentMap?.Broadcast(killPacket.Serialize());
            targetMonster.NextActionTime = DateTime.UtcNow.AddSeconds(10); // Respawn en 10s

            await ProcessExperienceAndLevelUpAsync(client, targetMonster);
        }
        else
        {
            var hitPacket = new CombatHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = 0,
                TargetHp = targetMonster.CurrentHp,
                TargetMaxHp = targetMonster.Stats.MaxHp
            };

            client.CurrentMap?.Broadcast(hitPacket.Serialize());
        }
    }

    // ==========================================
    // 2. ATAQUE CON HABILIDAD (F1, F2)
    // ==========================================
    public static async Task HandleSkillRequestAsync(GameClient client, GamePacketReader packet)
    {
        byte skillSlot = packet.ReadByte();
        uint attackerHandle = packet.ReadUInt();
        uint targetHandle = packet.ReadUInt();

        Console.WriteLine($"[DEBUG] Petición Habilidad recibida | Atacante: {attackerHandle:X} | Target: {targetHandle:X} | Slot: {skillSlot}");

        var targetMonster = client.CurrentMap?.GetMonster(targetHandle);
        var player = client.Player;

        if (targetMonster == null || targetMonster.IsDead || player == null)
            return;

        // 2.1 CONSUMO DE DS
        int dsCost = 15; // TODO: Leer de los assets según la habilidad usada

        if (player.PartnerStats.CurrentDs < dsCost)
        {
            Console.WriteLine($"[DEBUG] Ignorado: El Digimon no tiene suficiente DS. (Actual: {player.PartnerStats.CurrentDs}, Requiere: {dsCost})");
            return;
        }

        player.PartnerStats.UseDs(dsCost);

        // 2.2 CÁLCULO DE DAÑO
        int skillPower = 500; // TODO: Leer daño base de la habilidad desde los assets
        int attackerAtk = player.PartnerStats.Attack;
        int targetDef = targetMonster.Stats.Defense;

        int baseDamage = Math.Max(1, (skillPower + attackerAtk) - targetDef);
        int variation = Random.Shared.Next(-5, 6);
        int finalDamage = Math.Max(1, baseDamage + variation);

        // 2.3 APLICAR DAÑO
        bool isKilled = targetMonster.TakeDamage(finalDamage);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[✨ Habilidad] Slot {skillSlot} en {targetHandle:X} por {finalDamage} de daño. (DS restante: {player.PartnerStats.CurrentDs})");
        Console.ResetColor();

        byte hpRate = (byte)Math.Max(0, (targetMonster.CurrentHp * 100) / targetMonster.Stats.MaxHp);

        // 2.4 PONER EN COMBATE (opcode 1034)
        if (UseCombatOnPackets)
        {
            var on1 = new SetCombatOnPacket(attackerHandle).Serialize();
            PacketLog.Out(1034, on1);
            client.CurrentMap?.Broadcast(on1);

            var on2 = new SetCombatOnPacket(targetHandle).Serialize();
            PacketLog.Out(1034, on2);
            client.CurrentMap?.Broadcast(on2);
        }

        // 2.5 ENVIAR LA ANIMACIÓN DE LA SKILL (SIEMPRE se debe enviar el 1015)
        var cast = new CastSkillPacket(skillSlot, attackerHandle, targetHandle).Serialize();
        PacketLog.Out(1015, cast);
        client.CurrentMap?.Broadcast(cast);

        // 2.6 RESOLVER EL IMPACTO / DAÑO
        if (isKilled)
        {
            // Golpe mortal: notificar muerte por skill (1021)
            var kill = new KillOnSkillPacket(attackerHandle, targetHandle, skillSlot, finalDamage).Serialize();
            PacketLog.Out(1021, kill);
            client.CurrentMap?.Broadcast(kill);

            targetMonster.NextActionTime = DateTime.UtcNow.AddSeconds(10); // Respawn en 10s
            await ProcessExperienceAndLevelUpAsync(client, targetMonster);
        }
        else
        {
            // Golpe normal: impacto y actualización de vida (1102)
            var hit = new SkillHitPacket(attackerHandle, targetHandle, skillSlot, finalDamage, hpRate).Serialize();
            PacketLog.Out(1102, hit);
            client.CurrentMap?.Broadcast(hit);
        }
    }

    // ==========================================
    // 3. LÓGICA DE EXPERIENCIA Y SUBIDA DE NIVEL
    // ==========================================
    private static async Task ProcessExperienceAndLevelUpAsync(GameClient client, MonsterEntity targetMonster)
    {
        var player = client.Player;
        if (player == null) return;

        long digimonExp = targetMonster.Stats.DigimonExpReward;
        long tamerExp = Math.Max(1, (long)(digimonExp * 0.10));

        player.CurrentExp += digimonExp;
        player.TamerCurrentExp += tamerExp;

        // 1. Resolver TODOS los niveles ganados (el sobrante se arrastra)
        int partnerLevelsGained = 0;
        while (player.CurrentExp >= ExpTable.DigimonNeeded(player.PartnerLevel))
        {
            player.CurrentExp -= ExpTable.DigimonNeeded(player.PartnerLevel);
            player.PartnerLevel++;
            player.PartnerStats.Level = (byte)player.PartnerLevel;
            partnerLevelsGained++;
        }

        int tamerLevelsGained = 0;
        while (player.TamerCurrentExp >= ExpTable.TamerNeeded(player.TamerLevel))
        {
            player.TamerCurrentExp -= ExpTable.TamerNeeded(player.TamerLevel);
            player.TamerLevel++;
            tamerLevelsGained++;
        }

        // 2. Enviar la exp ya con el sobrante final
        await client.SendAsync(new ExpUpdatePacket(
            tamerExp, 0, player.TamerCurrentExp,
            player.PartnerHandle, digimonExp, 0, player.CurrentExp, 0).Serialize());

        // 3. Notificar el nivel final (una sola vez por entidad)
        if (partnerLevelsGained > 0)
            client.CurrentMap?.Broadcast(new LevelUpPacket(player.PartnerHandle, (byte)player.PartnerLevel).Serialize());
        if (tamerLevelsGained > 0)
            client.CurrentMap?.Broadcast(new LevelUpPacket(player.TamerHandle, (byte)player.TamerLevel).Serialize());
    }
}