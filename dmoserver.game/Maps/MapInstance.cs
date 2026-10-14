namespace dmoserver.game.Maps;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using dmoserver.game.Entities;
using dmoserver.game.Entities.Stats;
using dmoserver.game.Network;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Entities;

public class MapInstance
{
    public int MapId { get; set; }
    public ConcurrentDictionary<uint, MonsterEntity> Monsters { get; } = new();
    public ConcurrentDictionary<string, GameClient> Players { get; } = new();

    private readonly Random _rand = new();
    private CancellationTokenSource _cts = new();

    public MapInstance(int mapId)
    {
        MapId = mapId;
        InitMapEntities();
        StartRoamLoop();
    }

    private void InitMapEntities() { }

    public async Task LoadSpawnsAsync(dmoserver.database.MongoDbContext db)
    {
        if (Monsters.Count > 0) return;

        var mapSpawns = await db.GetSpawnsByMapIdAsync(MapId);
        uint idx = 1;

        foreach (var spawn in mapSpawns)
        {
            if (idx > 0x3FFF) break;
            uint mobHandle = 0x10000u | idx++;

            // 1. Buscamos la plantilla en la Base de Datos
            var template = await db.GetMonsterTemplateAsync((uint)spawn.MonsterId);

            // 2. Si existe en la BD, usamos sus stats. Si no, ponemos unos por defecto para que no crashee.
            DigimonStats stats;
            if (template != null)
            {
                stats = DigimonStats.FromTemplate(template, (short)template.Level, 10000);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Spawn] Mob cargado de BD: ID {spawn.MonsterId} ({template.Name}) - HP: {stats.MaxHp}, LV: {stats.Level}, AT: {stats.Attack}, DE: {stats.Defense}, CT: {stats.CriticalRate:F1}%, EV: {stats.EvasionRate:F1}%, Range: {stats.AttackRange}");
                Console.ResetColor();
            }
            else
            {
                stats = DigimonStats.FromTemplate(null, 3, 10000);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Spawn] AVISO: Mob ID {spawn.MonsterId} NO existe en 'monster_templates'. Fallback a HP: {stats.MaxHp}, LV: {stats.Level}.");
                Console.ResetColor();
            }

            // 3. Creamos la entidad
            Monsters.TryAdd(mobHandle, new MonsterEntity(
                mobHandle,
                (uint)spawn.MonsterId,
                spawn.X,
                spawn.Y,
                stats
            ));
        }
    }

    // Parámetros de combate y persecución de los mobs
    private const int DefaultAttackRange = 250;
    private const int MaxLeashDistance = 1800;
    private const int CombatSightRange = 2000;
    private static readonly TimeSpan CombatIdleTimeout = TimeSpan.FromSeconds(10);

    private void StartRoamLoop()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (!_cts.Token.IsCancellationRequested && await timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    UpdateRoaming();
                    await UpdateCombatStatesAsync();
                    await UpdatePlayerRegenerationAsync();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error RoamLoop] {ex.Message}");
                    Console.ResetColor();
                }
            }
        });
    }

    /// <summary>
    /// Gestiona el combate de los jugadores: auto-ataques periódicos según AttackSpeed del Partner,
    /// sincronización de persecución y salida de combate si el objetivo muere o se aleja.
    /// </summary>
    private async Task UpdateCombatStatesAsync()
    {
        var now = DateTime.UtcNow;

        foreach (var client in Players.Values)
        {
            var player = client.Player;
            if (player == null || !player.InCombat) continue;

            if (player.PartnerStats.CurrentHp <= 0)
            {
                var mob = GetMonster(player.CombatTargetHandle);
                dmoserver.game.Handlers.CombatHandler.LeaveCombat(client, mob);
                continue;
            }

            var target = GetMonster(player.CombatTargetHandle);

            if (target == null || target.IsDead)
            {
                dmoserver.game.Handlers.CombatHandler.LeaveCombat(client, target);
                continue;
            }

            int partnerX = player.PartnerX != 0 ? player.PartnerX : player.X;
            int partnerY = player.PartnerY != 0 ? player.PartnerY : player.Y;
            double dx = target.X - partnerX;
            double dy = target.Y - partnerY;
            double distSq = dx * dx + dy * dy;

            if (distSq > (double)CombatSightRange * CombatSightRange)
            {
                dmoserver.game.Handlers.CombatHandler.LeaveCombat(client, target);
                continue;
            }

            int attackRange = player.PartnerStats.AttackRange > 0 ? player.PartnerStats.AttackRange : DefaultAttackRange;
            int maxCombatRange = attackRange + 350;

            if (distSq <= (double)maxCombatRange * maxCombatRange)
            {
                if (now >= player.NextAutoAttackUtc)
                {
                    await dmoserver.game.Handlers.CombatHandler.ExecutePartnerAttackAsync(client, target);
                }
            }
        }
    }

    /// <summary>
    /// Regeneración pasiva gradual de HP y DS cada 6 segundos cuando el jugador está fuera de combate.
    /// Recupera un porcentaje proporcional (3% HP y 4% DS del Partner) para que el jugador se reponga naturalmente.
    /// </summary>
    private async Task UpdatePlayerRegenerationAsync()
    {
        var now = DateTime.UtcNow;

        foreach (var client in Players.Values)
        {
            var player = client.Player;
            if (player == null) continue;

            // Solo regenera fuera de combate y si el Partner sigue con vida
            if (player.InCombat || player.PartnerStats.CurrentHp <= 0) continue;

            if (player.NextRegenUtc == default)
            {
                player.NextRegenUtc = now.AddSeconds(6);
                continue;
            }

            if (now >= player.NextRegenUtc)
            {
                player.NextRegenUtc = now.AddSeconds(6);

                bool needsHp = player.PartnerStats.CurrentHp < player.PartnerStats.MaxHp;
                bool needsDs = player.PartnerStats.CurrentDs < player.PartnerStats.MaxDs;

                if (needsHp || needsDs)
                {
                    int hpGain = Math.Max(10, (int)Math.Ceiling(player.PartnerStats.MaxHp * 0.03));
                    int dsGain = Math.Max(8, (int)Math.Ceiling(player.PartnerStats.MaxDs * 0.04));

                    player.PartnerStats.RecoverHp(hpGain);
                    player.PartnerStats.RecoverDs(dsGain);

                    // Sincronizar barras con el cliente (1023)
                    var statusPacket = new dmoserver.game.Packets.UpdateStatusPacket(
                        player.PartnerHandle,
                        player.PartnerStats.CurrentHp,
                        player.PartnerStats.MaxHp,
                        player.PartnerStats.CurrentDs,
                        player.PartnerStats.MaxDs
                    ).Serialize();
                    await client.SendAsync(statusPacket);

                    byte partnerHpRate = (byte)Math.Clamp((player.PartnerStats.CurrentHp * 100) / Math.Max(1, player.PartnerStats.MaxHp), 0, 100);
                    Broadcast(new dmoserver.game.Packets.Combat.UpdateCurrentHPRatePacket(player.PartnerHandle, partnerHpRate).Serialize());
                }
            }
        }
    }

    private void UpdateRoaming()
    {
        var now = DateTime.UtcNow;

        foreach (var mob in Monsters.Values)
        {
            if (mob.IsDead)
            {
                if (now >= mob.NextActionTime)
                {
                    // Restauramos usando las Stats
                    mob.CurrentHp = mob.Stats.MaxHp;
                    mob.X = mob.SpawnX;
                    mob.Y = mob.SpawnY;
                    mob.State = MobState.Idle;
                    mob.InCombat = false;
                    mob.TargetPlayerAccount = null;

                    mob.NextActionTime = now.AddSeconds(3);

                    // 1. Sincronizar reaparición física (1006)
                    var spawnPacket = new MobSpawnPacket(
                        mob.Handle, 
                        (int)mob.ModelId, 
                        mob.SpawnX, 
                        mob.SpawnY, 
                        (byte)mob.Stats.Level
                    ).Serialize();
                    Broadcast(spawnPacket);
                    Broadcast(new dmoserver.game.Packets.Combat.UpdateCurrentHPRatePacket(mob.Handle, 100).Serialize());

                    // 2. Activación de estado Idle (1070)
                    var statePacket = new MobStatePacket(mob.Handle).Serialize();
                    Broadcast(statePacket);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[♻️ Respawn] Mob 0x{mob.Handle:X} (Modelo {mob.ModelId}) reapareció en X:{mob.SpawnX} Y:{mob.SpawnY} con {mob.CurrentHp} HP.");
                    Console.ResetColor();
                }
                continue;
            }

            if (mob.State == MobState.Combat)
            {
                if (mob.TargetPlayerAccount != null && Players.TryGetValue(mob.TargetPlayerAccount, out var targetClient) && targetClient.Player != null)
                {
                    var player = targetClient.Player;

                    // Si el Digimon compañero ha muerto, el mob deja de atacar y vuelve a su sitio
                    if (player.PartnerStats.CurrentHp <= 0)
                    {
                        ReturnMobToSpawn(mob, targetClient, now, "el Partner ha muerto");
                        continue;
                    }

                    // El objetivo del mob es el DIGIMON COMPAÑERO (Partner), quien lucha
                    int partnerX = player.PartnerX != 0 ? player.PartnerX : player.X;
                    int partnerY = player.PartnerY != 0 ? player.PartnerY : player.Y;

                    // Distancia actual entre el mob y el Partner
                    double dx = partnerX - mob.X;
                    double dy = partnerY - mob.Y;
                    double distToTarget = Math.Sqrt(dx * dx + dy * dy);

                    // Distancia entre el mob y su punto original de spawn
                    double spawnDx = mob.X - mob.SpawnX;
                    double spawnDy = mob.Y - mob.SpawnY;
                    double distFromSpawn = Math.Sqrt(spawnDx * spawnDx + spawnDy * spawnDy);

                    // 1. SI SE ALEJA DEMASIADO: El mob se retira (Leash / Reset)
                    if (distToTarget > CombatSightRange || distFromSpawn > MaxLeashDistance)
                    {
                        ReturnMobToSpawn(mob, targetClient, now,
                            $"Partner a {distToTarget:F0}u, Spawn a {distFromSpawn:F0}u");
                        continue;
                    }

                    int attackRange = mob.Stats.AttackRange > 0 ? mob.Stats.AttackRange : DefaultAttackRange;

                    // 2. SI ESTÁ FUERA DE RANGO CUERPO A CUERPO: Perseguir al Partner corriendo
                    if (distToTarget > attackRange)
                    {
                        int chaseSpeed = mob.Stats.MoveSpeed > 0 ? mob.Stats.MoveSpeed : 450;
                        // Avanzar hacia el Partner pero detenerse en el umbral del rango de ataque
                        double moveDistance = Math.Min(chaseSpeed, distToTarget - (attackRange * 0.8));

                        if (moveDistance > 0 && distToTarget > 0.001)
                        {
                            mob.X += (int)(dx / distToTarget * moveDistance);
                            mob.Y += (int)(dy / distToTarget * moveDistance);
                            mob.Yaw = (float)Math.Atan2(dy, dx);
                        }

                        // Se envía como destino la posición REAL del mob en el servidor (fin del paso),
                        // no la del Partner: así cliente y servidor coinciden y el mob no ataca "de lejos".
                        var runPacket = new MobSyncPositionPacket(mob.Handle, mob.X, mob.Y, action: 6).Serialize();
                        Broadcast(runPacket);
                        continue;
                    }

                    // 3. SI ESTÁ EN RANGO CUERPO A CUERPO (distToTarget <= attackRange): Atacar al Partner
                    if (now >= mob.NextAttackTime)
                    {
                        dmoserver.game.Handlers.CombatHandler.ProcessMobCounterAttack(targetClient, mob);
                        int attackSpeedMs = mob.Stats.AttackSpeed > 0 ? mob.Stats.AttackSpeed : 2000;
                        mob.NextAttackTime = now.AddMilliseconds(attackSpeedMs);
                    }
                }
                else
                {
                    // Objetivo no encontrado o desconectado
                    ReturnMobToSpawn(mob, null, now, "objetivo desconectado");
                }
                continue;
            }

            if (mob.State != MobState.Idle || now < mob.NextActionTime)
                continue;

            if (_rand.Next(0, 2) == 0)
            {
                mob.NextActionTime = now.AddSeconds(_rand.Next(4, 9));
                continue;
            }

            double angle = _rand.NextDouble() * Math.PI * 2;
            double distance = _rand.Next(200, mob.RoamRadius);

            int targetX = mob.SpawnX + (int)(Math.Cos(angle) * distance);
            int targetY = mob.SpawnY + (int)(Math.Sin(angle) * distance);
            float yaw = (float)Math.Atan2(targetY - mob.Y, targetX - mob.X);

            mob.X = targetX;
            mob.Y = targetY;
            mob.Yaw = yaw;
            mob.NextActionTime = now.AddSeconds(_rand.Next(6, 12));

            var movePacket = new MobSyncPositionPacket(mob.Handle, targetX, targetY).Serialize();
            Broadcast(movePacket);
        }
    }

    /// <summary>
    /// El mob abandona el combate y regresa caminando a su spawn. Deja SIEMPRE un estado coherente
    /// (State=Idle, InCombat=false, sin objetivo) y avisa al cliente con 1035 + paquete de caminar.
    /// </summary>
    private void ReturnMobToSpawn(MonsterEntity mob, GameClient? targetClient, DateTime now, string reason)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"[🏃 Retirada] Mob 0x{mob.Handle:X} abandona combate ({reason}). Regresando a spawn.");
        Console.ResetColor();

        if (targetClient != null)
        {
            // Saca del combate al mob y al Digimon (1035) y limpia el objetivo
            dmoserver.game.Handlers.CombatHandler.LeaveCombat(targetClient, mob);
        }

        if (mob.InCombat)
        {
            mob.InCombat = false;
            if (!mob.IsDead)
                Broadcast(new dmoserver.game.Packets.Combat.SetCombatOffPacket(mob.Handle).Serialize());
        }

        mob.State = MobState.Idle;
        mob.TargetPlayerAccount = null;
        mob.X = mob.SpawnX;
        mob.Y = mob.SpawnY;
        mob.NextActionTime = now.AddSeconds(4);

        // Acción 5 = Walk hacia el spawn
        Broadcast(new MobSyncPositionPacket(mob.Handle, mob.SpawnX, mob.SpawnY, action: 5).Serialize());
    }

    public void Broadcast(byte[] packet)
    {
        foreach (var client in Players.Values)
            _ = client.SendAsync(packet);
    }

    public MonsterEntity? GetMonster(uint handle)
    {
        Monsters.TryGetValue(handle, out var monster);
        return monster;
    }
}