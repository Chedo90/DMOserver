namespace dmoserver.game.Entities;

using dmoserver.database;
using dmoserver.game.Entities.Stats; // Añadir import

public sealed class PlayerEntity
{
    public uint AccountId { get; set; }
    public string? SessionToken { get; set; }
    public GameAccount? Account { get; set; }
    public CharacterDocument? Character { get; set; }
    public string? SelectedCharacterName { get; set; }

    // Estadísticas activas del Digimon compañero
    public DigimonStats PartnerStats { get; set; } = new DigimonStats
    {
        Level = 1,
        MaxHp = 620,
        MaxDs = 240,
        Attack = 135,       // AT base real de tu Digimon
        Defense = 28,
        AttackSpeed = 2000,
        HitRate = 500,
        CriticalRate = 5.0f
    };

    // Posición y orientación en tiempo real
    public int X { get; set; }
    public int Y { get; set; }
    public float Yaw { get; set; }

    // Estado del mapa y targeting
    public int LastLoadedMapId { get; set; } = -1;
    public uint TargetHandle { get; set; }

    // Handles dinámicos oficiales de GDMO
    public uint TamerHandle => Character != null ? (uint)(0x8000 + Character.Slot) : 0x8000;
    public uint PartnerHandle => Character != null ? (uint)(0x4000 + Character.Slot) : 0x4000;
}