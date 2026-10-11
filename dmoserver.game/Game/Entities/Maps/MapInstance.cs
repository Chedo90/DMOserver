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
        uint idx = 0;

        foreach (var spawn in mapSpawns)
        {
            if (idx > 0x3FFF) break;
            uint mobHandle = 0x10000u | idx++;

            // 1. Buscamos la plantilla en la Base de Datos
            var template = await db.GetMonsterTemplateAsync((uint)spawn.MonsterId);

            // 2. Si existe en la BD, usamos sus stats. Si no, ponemos unos por defecto para que no crashee.
            var stats = template != null ? new DigimonStats
            {
                Level = (byte)template.Level,
                MaxHp = template.MaxHp,
                Attack = template.Attack,
                Defense = template.Defense,
                ExpReward = template.ExpReward
            } : new DigimonStats
            {
                Level = 3,
                MaxHp = 1000,
                Attack = 50,
                Defense = 10,
                ExpReward = 10
            };

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

    private void StartRoamLoop()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!_cts.Token.IsCancellationRequested && await timer.WaitForNextTickAsync(_cts.Token))
            {
                UpdateRoaming();
            }
        });
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

                    mob.NextActionTime = now.AddSeconds(3);

                    // Sincronizar reaparición pasando el nivel real de la entidad
                    var spawnPacket = new MobSpawnPacket(
                        mob.Handle, 
                        (int)mob.ModelId, 
                        mob.SpawnX, 
                        mob.SpawnY, 
                        (byte)mob.Stats.Level
                    ).Serialize();

                    Broadcast(spawnPacket);
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