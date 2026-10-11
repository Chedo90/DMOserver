namespace dmoserver.game.Entities;

using System;
using dmoserver.game.Entities.Stats;

public enum MobState
{
    Idle,
    Roaming,
    Combat
}

public class MonsterEntity
{
    private readonly object _hpLock = new();

    public uint Handle { get; set; }
    public uint ModelId { get; set; }
    
    // Contenedor unificado de estadísticas
    public DigimonStats Stats { get; set; } 
    public int CurrentHp { get; set; }
    public bool IsDead => CurrentHp <= 0;

    public int X { get; set; }
    public int Y { get; set; }
    public float Yaw { get; set; }
    public int SpawnX { get; }
    public int SpawnY { get; }
    public int RoamRadius { get; set; } = 700;
    public MobState State { get; set; } = MobState.Idle;
    public DateTime NextActionTime { get; set; } = DateTime.UtcNow;

    public MonsterEntity(uint handle, uint modelId, int x, int y, DigimonStats? stats = null)
    {
        Handle = handle;
        ModelId = modelId;
        X = x;
        Y = y;
        SpawnX = x;
        SpawnY = y;

        // Si se pasan stats, se usan directamente; de lo contrario, se crea un fallback base
        Stats = stats ?? new DigimonStats
        {
            Level = 1,
            MaxHp = 1200,
            Attack = 60,
            Defense = 15,
            ExpReward = 45
        };

        CurrentHp = Stats.MaxHp;
    }

    public bool TakeDamage(int damage)
    {
        lock (_hpLock)
        {
            CurrentHp -= damage;
            if (CurrentHp < 0) CurrentHp = 0;
            return CurrentHp == 0;
        }
    }
}