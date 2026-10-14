namespace dmoserver.game.Handlers;

using dmoserver.game.Entities;
using dmoserver.game.Entities.Stats;
using dmoserver.game.Network;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Combat;
using dmoserver.game.Packets.Digimon;
using dmoserver.game.Handlers.Combat;
using System;
using System.Threading.Tasks;

public static class CombatHandler
{

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

        if (client.Player == null || client.Player.PartnerStats.CurrentHp <= 0)
        {
            Console.WriteLine("[DEBUG] Ignorado: el Digimon partner está muerto y no puede atacar.");
            return;
        }

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

        EnterCombat(client, targetMonster);

        int partnerX = client.Player.PartnerX != 0 ? client.Player.PartnerX : client.Player.X;
        int partnerY = client.Player.PartnerY != 0 ? client.Player.PartnerY : client.Player.Y;
        double dx = targetMonster.X - partnerX;
        double dy = targetMonster.Y - partnerY;
        double distSq = dx * dx + dy * dy;

        int attackRange = client.Player.PartnerStats.AttackRange > 0 ? client.Player.PartnerStats.AttackRange : 250;
        int maxRange = attackRange + 350;

        if (distSq <= (double)maxRange * maxRange)
        {
            await ExecutePartnerAttackAsync(client, targetMonster);
        }
        else
        {
            // Si está a distancia, queda listo para golpear tan pronto como el partner alcance el rango
            client.Player.NextAutoAttackUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Ejecuta un ataque básico del Digimon Partner hacia el monstruo objetivo (usado tanto para el hit manual como para el bucle de auto-ataque periódico).
    /// </summary>
    public static async Task ExecutePartnerAttackAsync(GameClient client, MonsterEntity targetMonster)
    {
        var player = client.Player;
        var map = client.CurrentMap;
        if (player == null || map == null || targetMonster.IsDead) return;

        uint attackerHandle = player.PartnerHandle;
        uint targetHandle = targetMonster.Handle;

        // 1. Programar el siguiente auto-ataque según la velocidad de ataque del Partner (AttackSpeed en ms)
        int attackSpeedMs = player.PartnerStats.AttackSpeed > 0 ? player.PartnerStats.AttackSpeed : 2000;
        player.NextAutoAttackUtc = DateTime.UtcNow.AddMilliseconds(attackSpeedMs);
        player.LastCombatActionUtc = DateTime.UtcNow;

        // 1.5 Comprobar Fallo (Miss) según Hit Rate (HT) del Partner vs Evasión (EV) del Mob
        if (CombatCalculator.CanMissHit(player.PartnerStats, targetMonster.Stats))
        {
            var missPacket = new MissHitPacket(attackerHandle, targetHandle);
            map.Broadcast(missPacket.Serialize());
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[💨 MISS] Partner {attackerHandle:X} falló su ataque contra 0x{targetHandle:X}.");
            Console.ResetColor();
            return;
        }

        // 2. Calcular daño usando el motor oficial de ODMO (crítico x2.0, bloqueo /2, ventajas de atributo y elemento)
        var (damage, hitType) = CombatCalculator.CalculateDamage(player.PartnerStats, targetMonster.Stats);

        int hpBeforeHit = targetMonster.CurrentHp;
        bool isKilled = damage > 0 && targetMonster.TakeDamage(damage);

        string hitLabel = hitType switch { 1 => " [CRÍTICO x2.0]", 2 => " [BLOQUEADO]", _ => "" };
        Console.ForegroundColor = isKilled ? ConsoleColor.Yellow : (hitType == 1 ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        Console.WriteLine($"[⚔️ Auto-Ataque]{hitLabel} Partner {attackerHandle:X} golpea a {targetHandle:X} por {damage} daño (HP: {targetMonster.CurrentHp}/{targetMonster.Stats.MaxHp}). Muerto: {isKilled}");
        Console.ResetColor();

        if (isKilled)
        {
            var killPacket = new KillOnHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = hitType
            };

            map.Broadcast(killPacket.Serialize());
            map.Broadcast(new UpdateCurrentHPRatePacket(targetHandle, 0).Serialize());
            targetMonster.NextActionTime = DateTime.UtcNow.AddSeconds(10); // Respawn en 10s

            LeaveCombat(client, targetMonster);
            await ProcessExperienceAndLevelUpAsync(client, targetMonster);
        }
        else
        {
            byte hpRate = (byte)Math.Clamp((targetMonster.CurrentHp * 100) / Math.Max(1, targetMonster.Stats.MaxHp), 0, 100);
            var hitPacket = new CombatHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = hitType,
                HpAfterHit = targetMonster.CurrentHp,
                HpBeforeHit = hpBeforeHit
            };

            map.Broadcast(hitPacket.Serialize());
            map.Broadcast(new UpdateCurrentHPRatePacket(targetHandle, hpRate).Serialize());
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

        var player = client.Player;
        if (player == null) return;

        // 2.0 NORMALIZAR HANDLES PARA EVITAR DESINCRONIZACIÓN
        if (attackerHandle == 0 || attackerHandle != player.PartnerHandle)
            attackerHandle = player.PartnerHandle;

        if (targetHandle == 0 && player.TargetHandle != 0)
            targetHandle = player.TargetHandle;

        var targetMonster = client.CurrentMap?.GetMonster(targetHandle);

        if (targetMonster == null || targetMonster.IsDead)
        {
            Console.WriteLine($"[DEBUG] Habilidad ignorada: objetivo 0x{targetHandle:X} no válido o ya muerto.");
            return;
        }

        if (player.PartnerStats.CurrentHp <= 0)
        {
            Console.WriteLine("[DEBUG] Ignorado: el Digimon partner está muerto y no puede usar habilidades.");
            return;
        }

        // 2.1 RESOLUCIÓN DE LA HABILIDAD (Slot 0 en paquete = F1, Slot 1 = F2...)
        int slot = skillSlot + 1;
        int skillId = slot switch
        {
            1 => player.PartnerStats.Skill1,
            2 => player.PartnerStats.Skill2,
            3 => player.PartnerStats.Skill3,
            4 => player.PartnerStats.Skill4,
            _ => 0
        };

        int digimonModel = player.Character?.Partner?.Model ?? 31001;
        if (skillId == 0)
        {
            // Fórmula oficial DMO estándar si aún no está asignado:
            // Modelo * 100 + Slot * 10 + 1 (ej: Agumon 31001 -> F1: 3100111, F2: 3100121)
            skillId = (digimonModel * 100) + (slot * 10) + 1;
        }

        // Consultar plantilla oficial en MongoDB (colección 'skill_templates')
        var skillTemplate = await GameClient.Db.GetSkillTemplateAsync(skillId);

        // Stats de la habilidad (con valores oficiales por defecto si aún no está en MongoDB)
        int baseSkillPower = skillTemplate?.BasePower ?? 0;
        if (baseSkillPower <= 0)
        {
            baseSkillPower = slot == 1 ? 346 : 772;
        }

        int increasePower = skillTemplate?.IncreasePower ?? 0;
        if (increasePower <= 0)
        {
            increasePower = slot == 1 ? 20 : 45; // Incremento oficial DMO por nivel de habilidad
        }

        byte skillLevel = player.GetSkillLevel(slot);
        int totalSkillPower = baseSkillPower + Math.Max(0, skillLevel - 1) * increasePower;

        int dsCost = skillTemplate?.UseDs ?? 0;
        if (dsCost <= 0)
        {
            dsCost = slot == 1 ? 18 : 36;
        }

        int cooldownSeconds = (int)(skillTemplate?.CooldownTime ?? 0);
        if (cooldownSeconds <= 0)
        {
            cooldownSeconds = slot == 1 ? 3 : 6;
        }

        string skillName = !string.IsNullOrWhiteSpace(skillTemplate?.Name)
            ? skillTemplate.Name
            : (slot == 1 ? "Skill F1" : "Skill F2");

        // 2.2 CONSUMO DE DS
        if (player.PartnerStats.CurrentDs < dsCost)
        {
            Console.WriteLine($"[DEBUG] Ignorado: El Digimon no tiene suficiente DS. (Actual: {player.PartnerStats.CurrentDs}, Requiere: {dsCost})");
            return;
        }

        player.PartnerStats.UseDs(dsCost);

        // Notificar al cliente la actualización del DS consumido
        await client.SendAsync(new UpdateStatusPacket(
            player.PartnerHandle,
            player.PartnerStats.CurrentHp,
            player.PartnerStats.MaxHp,
            player.PartnerStats.CurrentDs,
            player.PartnerStats.MaxDs
        ).Serialize());

        // 2.3 CÁLCULO DE DAÑO CON MOTOR OFICIAL (incluye crítico x2.0, bloqueo, atributos, elementos y escala por nivel)
        var (finalDamage, hitType) = CombatCalculator.CalculateDamage(player.PartnerStats, targetMonster.Stats, totalSkillPower);

        // 2.4 APLICAR DAÑO
        bool isKilled = targetMonster.TakeDamage(finalDamage);

        string skillHitLabel = hitType switch { 1 => " [CRÍTICO x2.0]", 2 => " [BLOQUEADO]", _ => "" };
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[✨ Habilidad]{skillHitLabel} '{skillName}' Lv.{skillLevel} (Poder total: {totalSkillPower}, ID {skillId}) en 0x{targetHandle:X} por {finalDamage} de daño. (DS restante: {player.PartnerStats.CurrentDs}/{player.PartnerStats.MaxDs})");
        Console.ResetColor();

        byte hpRate = (byte)Math.Max(0, (targetMonster.CurrentHp * 100) / targetMonster.Stats.MaxHp);

        // 2.5 ENTRAR EN COMBATE (postura de combate del Digimon, opcode 1034)
        EnterCombat(client, targetMonster);

        // 2.6 ANIMACIÓN DE CASTING (Opcode 1015 - siempre se emite para que el Digimon ejecute la animación)
        var cast = new CastSkillPacket(skillSlot, attackerHandle, targetHandle).Serialize();
        PacketLog.Out(1015, cast);
        client.CurrentMap?.Broadcast(cast);

        // 2.7 IMPACTO O MUERTE
        if (isKilled)
        {
            var kill = new KillOnSkillPacket(attackerHandle, targetHandle, skillSlot, finalDamage).Serialize();
            PacketLog.Out(1021, kill);
            client.CurrentMap?.Broadcast(kill);
            client.CurrentMap?.Broadcast(new UpdateCurrentHPRatePacket(targetHandle, 0).Serialize());

            targetMonster.NextActionTime = DateTime.UtcNow.AddSeconds(10); // Respawn en 10s
            LeaveCombat(client, targetMonster);
            await ProcessExperienceAndLevelUpAsync(client, targetMonster);
        }
        else
        {
            var hit = new SkillHitPacket(attackerHandle, targetHandle, skillSlot, finalDamage, hpRate).Serialize();
            PacketLog.Out(1102, hit);
            client.CurrentMap?.Broadcast(hit);
            client.CurrentMap?.Broadcast(new UpdateCurrentHPRatePacket(targetHandle, hpRate).Serialize());
        }

        // 2.8 ENVIAR ENFRIAMIENTO (Opcode 3246 - activa la animación de cooldown en la hotbar)
        var cooldownPacket = new SkillUpdateCooldownPacket(
            player.PartnerHandle,
            digimonModel,
            skillId,
            cooldownSeconds
        ).Serialize();
        await client.SendAsync(cooldownPacket);
    }

    // ==========================================
    // ESTADO DE COMBATE (postura del Digimon)
    // ==========================================

    /// <summary>
    /// Pone al Digimon (y al mob, la primera vez) en postura de combate (1034) y refresca el temporizador.
    /// </summary>
    public static void EnterCombat(GameClient client, MonsterEntity target)
    {
        var player = client.Player;
        var map = client.CurrentMap;
        if (player == null || map == null) return;

        // Un Digimon muerto no puede entrar en combate
        if (player.PartnerStats.CurrentHp <= 0) return;

        if (player.PartnerX == 0 && player.PartnerY == 0)
        {
            player.PartnerX = player.X;
            player.PartnerY = player.Y;
        }

        if (!player.InCombat)
        {
            player.InCombat = true;
            var on = new SetCombatOnPacket(player.PartnerHandle).Serialize();
            PacketLog.Out(1034, on);
            map.Broadcast(on);
        }

        if (!target.InCombat)
        {
            target.InCombat = true;
            var onMob = new SetCombatOnPacket(target.Handle).Serialize();
            PacketLog.Out(1034, onMob);
            map.Broadcast(onMob);
        }

        // El estado se fuerza siempre a Combat (antes solo se hacía al cambiar InCombat y
        // podía quedar el mob en Idle con InCombat=true, ignorando al jugador).
        if (target.State != MobState.Combat)
        {
            target.State = MobState.Combat;
            int attackSpeedMs = target.Stats.AttackSpeed > 0 ? target.Stats.AttackSpeed : 2000;
            target.NextAttackTime = DateTime.UtcNow.AddMilliseconds(attackSpeedMs);
        }

        target.TargetPlayerAccount = client.EndPoint;
        player.CombatTargetHandle = target.Handle;
        player.LastCombatActionUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Saca al Digimon de la postura de combate (1035). Se llama al matar, al huir o al perder el mob la vista.
    /// </summary>
    public static void LeaveCombat(GameClient client, MonsterEntity? target)
    {
        var player = client.Player;
        var map = client.CurrentMap;
        if (player == null || map == null) return;

        if (target != null)
        {
            target.TargetPlayerAccount = null;
            target.State = MobState.Idle;
            if (target.InCombat)
            {
                target.InCombat = false;
                if (!target.IsDead)
                    map.Broadcast(new SetCombatOffPacket(target.Handle).Serialize());
            }
        }

        if (!player.InCombat) return;

        player.InCombat = false;
        player.CombatTargetHandle = 0;
        var off = new SetCombatOffPacket(player.PartnerHandle).Serialize();
        PacketLog.Out(1035, off);
        map.Broadcast(off);
    }

    /// <summary>
    /// Ejecuta el ataque de respuesta del monstruo hacia el Digimon del jugador.
    /// </summary>
    public static void ProcessMobCounterAttack(GameClient client, MonsterEntity mob)
    {
        var player = client.Player;
        var map = client.CurrentMap;
        if (player == null || map == null || mob.IsDead) return;

        // Un Digimon muerto no recibe más golpes
        if (player.PartnerStats.CurrentHp <= 0)
        {
            LeaveCombat(client, mob);
            return;
        }

        // 1. Comprobar si el ataque del mob falla (Miss) contra la evasión del partner
        if (CombatCalculator.CanMissHit(mob.Stats, player.PartnerStats))
        {
            var missPacket = new MissHitPacket(mob.Handle, player.PartnerHandle);
            map.Broadcast(missPacket.Serialize());
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[💨 MISS] Mob {mob.Handle:X} falló su ataque contra Partner {player.PartnerHandle:X}.");
            Console.ResetColor();
            return;
        }

        // 2. Calcular daño del contraataque usando el motor oficial de ODMO
        var (damage, hitType) = CombatCalculator.CalculateDamage(mob.Stats, player.PartnerStats);

        int hpBeforeHit = player.PartnerStats.CurrentHp;
        if (damage > 0)
        {
            player.PartnerStats.CurrentHp = Math.Max(0, player.PartnerStats.CurrentHp - damage);
        }
        bool partnerDied = player.PartnerStats.CurrentHp <= 0;

        string hitLabel = hitType switch { 1 => " [CRÍTICO]", 2 => " [MISS]", 3 => " [BLOQUEADO]", _ => "" };
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[💥 Contraataque]{hitLabel} Mob {mob.Handle:X} golpea a Partner {player.PartnerHandle:X} por {damage} daño. (HP restante: {player.PartnerStats.CurrentHp}/{player.PartnerStats.MaxHp})");
        Console.ResetColor();

        if (!partnerDied)
        {
            // Paquete de golpe (1013)
            var hitPacket = new CombatHitPacket
            {
                AttackerHandle = mob.Handle,
                TargetHandle = player.PartnerHandle,
                Damage = damage,
                HitType = hitType,
                HpAfterHit = player.PartnerStats.CurrentHp,
                HpBeforeHit = hpBeforeHit
            };

            map.Broadcast(hitPacket.Serialize());
            byte partnerHpRate = (byte)Math.Clamp((player.PartnerStats.CurrentHp * 100) / Math.Max(1, player.PartnerStats.MaxHp), 0, 100);
            map.Broadcast(new UpdateCurrentHPRatePacket(player.PartnerHandle, partnerHpRate).Serialize());
        }
        else
        {
            // Golpe mortal sobre el Digimon: 1020
            var killPacket = new KillOnHitPacket
            {
                AttackerHandle = mob.Handle,
                TargetHandle = player.PartnerHandle,
                Damage = damage,
                HitType = hitType
            };
            var killBytes = killPacket.Serialize();
            PacketLog.Out(1020, killBytes);
            map.Broadcast(killBytes);
            map.Broadcast(new UpdateCurrentHPRatePacket(player.PartnerHandle, 0).Serialize());

            player.PartnerStats.CurrentDs = 0;

            // Reducir amistad al morir (-5 puntos)
            player.PartnerStats.Friendship = Math.Max(0, player.PartnerStats.Friendship - 5);
            if (player.Account != null && player.Character != null)
            {
                _ = GameClient.Db.UpdatePartnerFriendshipAsync(
                    player.Account.AccountId, player.Character.Slot, player.PartnerStats.Friendship);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[☠️ Muerte] El Digimon partner {player.PartnerHandle:X} ha sido derrotado (Amistad: {player.PartnerStats.Friendship}).");
            Console.ResetColor();
        }

        // Actualizar barra de estado del Digimon en la UI del cliente (1023)
        var statusPacket = new UpdateStatusPacket(
            player.PartnerHandle,
            player.PartnerStats.CurrentHp,
            player.PartnerStats.MaxHp,
            player.PartnerStats.CurrentDs,
            player.PartnerStats.MaxDs
        );
        _ = client.SendAsync(statusPacket.Serialize());

        if (partnerDied)
            OnPartnerDied(client);
    }

    /// <summary>
    /// El Digimon partner ha muerto: todos los mobs que lo tenían como objetivo salen de combate
    /// y el Digimon sale de la postura de combate (1035).
    /// </summary>
    private static void OnPartnerDied(GameClient client)
    {
        var map = client.CurrentMap;
        if (map == null) return;

        foreach (var mob in map.Monsters.Values)
        {
            if (mob.TargetPlayerAccount == client.EndPoint)
                LeaveCombat(client, mob);
        }

        // Por si el jugador seguía marcado en combate sin mob asociado
        LeaveCombat(client, null);
    }

    // ==========================================
    // 3. LÓGICA DE EXPERIENCIA Y SUBIDA DE NIVEL
    // ==========================================
    private static async Task ProcessExperienceAndLevelUpAsync(GameClient client, MonsterEntity targetMonster)
    {
        var player = client.Player;
        if (player == null) return;

        // Calcular experiencia coherente con el nivel del monstruo (evitando que mobs de nivel bajo den miles de exp por XML desfasado)
        int mobLevel = Math.Max(1, (int)targetMonster.Stats.Level);
        long rawDigiExp = targetMonster.Stats.DigimonExpReward > 0 
            ? targetMonster.Stats.DigimonExpReward 
            : (targetMonster.Stats.ExpReward > 0 ? (long)targetMonster.Stats.ExpReward : 50);

        // Si la EXP de la plantilla es desproporcionada para el nivel del mob (ej: 2000 o 43888 en un mob lv 1-10),
        // ajustamos a la escala estándar de DMO / ODMO (~40-60 exp por nivel del mob).
        long digimonExp = rawDigiExp;
        if (mobLevel <= 20 && digimonExp > (mobLevel * 70))
        {
            digimonExp = Math.Max(30, (long)(mobLevel * 45));
        }

        // Reducción por diferencia de nivel si el jugador es muy superior al mob (fórmula oficial ODMO)
        int levelDiff = player.PartnerLevel - mobLevel;
        if (levelDiff >= 30)
        {
            digimonExp = 0; // Sin EXP si supera al mob por 30+ niveles
        }
        else if (levelDiff > 10)
        {
            double penalty = 1.0 - ((levelDiff - 10) * 0.04);
            digimonExp = Math.Max(1, (long)(digimonExp * Math.Max(0.1, penalty)));
        }

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

        // 3. Notificar el nivel final y recalcular estadísticas si el Partner subió de nivel
        if (partnerLevelsGained > 0)
        {
            var partnerDoc = player.Character?.Partner;
            var digimonTemplate = await GameClient.Db.GetDigimonTemplateAsync(partnerDoc?.Model ?? 0);
            player.PartnerStats = DigimonStats.FromDigimonTemplate(
                digimonTemplate,
                (short)player.PartnerLevel,
                partnerDoc?.Size ?? 10000,
                player.PartnerStats.Friendship
            );

            // Efecto visual y texto de Level Up
            client.CurrentMap?.Broadcast(new LevelUpPacket(player.PartnerHandle, (byte)player.PartnerLevel).Serialize());

            // Actualizar la hoja completa de estadísticas en la ventana C y HUD (Opcode oficial 1043)
            await client.SendAsync(new UpdateFullStatusPacket(player).Serialize());

            // Restaurar y refrescar barras de HP y DS (1023)
            await client.SendAsync(new UpdateStatusPacket(
                player.PartnerHandle,
                player.PartnerStats.CurrentHp,
                player.PartnerStats.MaxHp,
                player.PartnerStats.CurrentDs,
                player.PartnerStats.MaxDs
            ).Serialize());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[🎉 LEVEL UP] Partner '{partnerDoc?.Name}' subió a Nivel {player.PartnerLevel}! Stats -> HP: {player.PartnerStats.MaxHp} | AT: {player.PartnerStats.Attack} | DE: {player.PartnerStats.Defense}");
            Console.ResetColor();
        }

        if (tamerLevelsGained > 0)
        {
            client.CurrentMap?.Broadcast(new LevelUpPacket(player.TamerHandle, (byte)player.TamerLevel).Serialize());
            await client.SendAsync(new UpdateFullStatusPacket(player).Serialize());
        }

        // 4. Experiencia de Habilidad (Skill EXP) oficial por derrotar al monstruo
        int skillExpGain = Math.Max(15, mobLevel * 6);
        bool masteryUp = player.AddSkillExperience(skillExpGain);

        // Notificar al cliente con el paquete oficial 1105 (actualiza barra de Skill Exp y puntos disponibles)
        var skillProgressPacket = new UpdateSkillProgressPacket(
            player.PartnerHandle,
            1, // FormSlot: 1 (Rookie)
            player.SkillPoints,
            player.SkillMastery,
            player.SkillExperience
        ).Serialize();
        await client.SendAsync(skillProgressPacket);

        if (masteryUp)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[⭐ Skill Mastery Up] Partner '{player.Character?.Partner?.Name}' subió a Maestría {player.SkillMastery}! Puntos de habilidad disponibles: {player.SkillPoints}");
            Console.ResetColor();
        }

        // Guardar progreso completo (nivel, exp y skills) en MongoDB
        if (player.Account != null && player.Character != null)
        {
            _ = GameClient.Db.UpdateProgressAsync(
                player.Account.AccountId,
                player.Character.Slot,
                (short)player.TamerLevel,
                player.TamerCurrentExp,
                (short)player.PartnerLevel,
                player.CurrentExp
            );

            _ = GameClient.Db.UpdatePartnerSkillsAsync(
                player.Account.AccountId,
                player.Character.Slot,
                player.SkillPoints,
                player.SkillMastery,
                player.SkillExperience,
                player.GetSkillLevel(1),
                player.GetSkillLevel(2),
                player.GetSkillLevel(3),
                player.GetSkillLevel(4),
                player.GetSkillLevel(5)
            );
        }
    }
}