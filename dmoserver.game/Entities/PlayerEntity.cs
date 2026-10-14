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
        CurrentHp = 620,
        MaxDs = 240,
        CurrentDs = 240,
        Attack = 135,
        Defense = 28,
        AttackSpeed = 2000,
        HitRate = 500,
        CriticalRate = 5.0f
    };

    public long CurrentExp { get; set; }
    public int PartnerLevel { get; set; } = 1;
    public long TamerCurrentExp { get; set; }
    public int TamerLevel { get; set; } = 1;

    public long Bits
    {
        get => Character?.Bits ?? 0;
        set
        {
            if (Character != null) Character.Bits = value;
        }
    }
    public int X { get; set; }
    public int Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }

    // Coordenadas del Digimon compañero (Partner)
    public int PartnerX { get; set; }
    public int PartnerY { get; set; }
    public float PartnerZ { get; set; }

    public int LastLoadedMapId { get; set; } = -1;
    public uint TargetHandle { get; set; }

    // Estado de combate (postura del Digimon)
    public bool InCombat { get; set; }
    public uint CombatTargetHandle { get; set; }
    public DateTime LastCombatActionUtc { get; set; }
    public DateTime NextAutoAttackUtc { get; set; }
    public DateTime NextRegenUtc { get; set; }

    // --- Sistema de Habilidades (Skills, Maestría y Experiencia) ---
    public byte SkillPoints => Character?.Partner?.SkillPoints ?? 0;
    public byte SkillMastery => Character?.Partner?.SkillMastery ?? 0;
    public int SkillExperience => Character?.Partner?.SkillExperience ?? 0;

    public byte GetSkillLevel(int slot)
    {
        if (Character?.Partner == null) return 1;
        return slot switch
        {
            1 => Character.Partner.SkillLevel1 > 0 ? Character.Partner.SkillLevel1 : (byte)1,
            2 => Character.Partner.SkillLevel2 > 0 ? Character.Partner.SkillLevel2 : (byte)1,
            3 => Character.Partner.SkillLevel3,
            4 => Character.Partner.SkillLevel4,
            5 => Character.Partner.SkillLevel5,
            _ => 1
        };
    }

    public void SetSkillLevel(int slot, byte level)
    {
        if (Character?.Partner == null) return;
        switch (slot)
        {
            case 1: Character.Partner.SkillLevel1 = level; break;
            case 2: Character.Partner.SkillLevel2 = level; break;
            case 3: Character.Partner.SkillLevel3 = level; break;
            case 4: Character.Partner.SkillLevel4 = level; break;
            case 5: Character.Partner.SkillLevel5 = level; break;
        }
    }

    // Tabla oficial de experiencia de habilidad para Rookie (ODMO / DMO)
    private static readonly int[] RookieSkillExpTable =
    [
        281, 315, 352, 395, 442, 495, 555, 621, 696, 779,
        873, 977, 1095, 1226, 1373, 1538, 1722, 1930, 2160, 2420,
        2710, 3036, 3400, 3808, 4264, 4776, 5350, 5992, 6712, 7516, 8418
    ];

    /// <summary>
    /// Otorga experiencia de habilidad al Partner. Si alcanza el umbral necesario,
    /// sube la maestría y otorga 2 puntos de habilidad (+2 Skill Points).
    /// </summary>
    public bool AddSkillExperience(int expAmount)
    {
        if (Character?.Partner == null || expAmount <= 0) return false;

        var partner = Character.Partner;
        if (partner.SkillMastery >= 30) return false;

        partner.SkillExperience += expAmount;
        bool masteryGained = false;

        while (partner.SkillMastery < 30)
        {
            int needed = partner.SkillMastery < RookieSkillExpTable.Length
                ? RookieSkillExpTable[partner.SkillMastery]
                : 10000;

            if (partner.SkillExperience >= needed)
            {
                partner.SkillExperience -= needed;
                partner.SkillMastery++;
                partner.SkillPoints += 2; // +2 puntos oficiales de habilidad por cada maestría
                masteryGained = true;
            }
            else
            {
                break;
            }
        }

        return masteryGained;
    }

    public uint TamerHandle => Character != null ? (uint)(0x8000 + Character.Slot) : 0x8000;
    public uint PartnerHandle => Character != null ? (uint)(0x4000 + Character.Slot) : 0x4000;
}