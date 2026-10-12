using dmoserver.database;
using dmoserver.game.Entities.Stats;

namespace dmoserver.game.Entities;

public sealed class PlayerEntity
{
    public uint AccountId { get; set; }
    public string? SessionToken { get; set; }
    public GameAccount? Account { get; set; }
    public CharacterDocument? Character { get; set; }
    public string? SelectedCharacterName { get; set; }

    public DigimonStats PartnerStats { get; set; } = new DigimonStats
    {
        Level = 1,
        MaxHp = 620,
        MaxDs = 240,
        Attack = 135,
        Defense = 28,
        AttackSpeed = 2000,
        HitRate = 500,
        CriticalRate = 5.0f
    };

    // Make sure these properties are declared and public
    public long CurrentExp { get; set; }
    public int PartnerLevel { get; set; } = 1;
// Añadir a PlayerEntity.cs
public long TamerCurrentExp { get; set; }
public int TamerLevel { get; set; } = 1;
    public int X { get; set; }
    public int Y { get; set; }
    public float Yaw { get; set; }

    public int LastLoadedMapId { get; set; } = -1;
    public uint TargetHandle { get; set; }

    public uint TamerHandle => Character != null ? (uint)(0x8000 + Character.Slot) : 0x8000;
    public uint PartnerHandle => Character != null ? (uint)(0x4000 + Character.Slot) : 0x4000;
}