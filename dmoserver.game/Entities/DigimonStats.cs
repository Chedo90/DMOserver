namespace dmoserver.game.Entities.Stats;

using System;
using dmoserver.database;

public class DigimonStats
{
    // Nivel
    public byte Level { get; set; } = 1;

    // Recursos vitales
    public int MaxHp { get; set; } = 500;
    public int CurrentHp { get; set; } = 500;
    public int MaxDs { get; set; } = 200;
    public int CurrentDs { get; set; } = 200;

    // Amistad (FS: 0 a 100)
    public int Friendship { get; set; } = 20;

    // Ofensivos
    public int Attack { get; set; } = 120;       // AT
    public int AttackSpeed { get; set; } = 2000; // AS (milisegundos)
    public int AttackRange { get; set; } = 250;  // AR (alcance cuerpo a cuerpo o a distancia)
    public int MoveSpeed { get; set; } = 600;    // MS (velocidad de movimiento)
    public float CriticalRate { get; set; } = 5.0f; // CT (%)
    public int HitRate { get; set; } = 500;      // HT

    // Defensivos
    public int Defense { get; set; } = 35;       // DE
    public float BlockRate { get; set; } = 0.0f; // BL (%)
    public float EvasionRate { get; set; } = 0.0f; // EV (%)

    // Recompensa al morir (solo mobs)
    public int ExpReward { get; set; } = 50;

    // Experiencia configurada en base de datos
    public long TamerExpReward { get; set; }
    public long DigimonExpReward { get; set; }

    // Atributo y Elemento (Ventajas y Debilidades)
    public int AttributeType { get; set; } = 1; // 1=None, 2=Data, 3=Vaccine, 4=Virus, 5=Unknown
    public int NatureType { get; set; } = 0;    // 0=Neutral, 16=Ice, 17=Water, 18=Fire, etc.

    // Habilidades / Skills (F1 a F4)
    public int Skill1 { get; set; }
    public int Skill2 { get; set; }
    public int Skill3 { get; set; }
    public int Skill4 { get; set; }

    // Gastar DS
    public void UseDs(int amount)
    {
        if (amount <= 0) return;
        CurrentDs = Math.Max(0, CurrentDs - amount);
    }

    // Recuperar HP
    public void RecoverHp(int amount)
    {
        if (amount <= 0 || CurrentHp <= 0) return;
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
    }

    // Recuperar DS
    public void RecoverDs(int amount)
    {
        if (amount <= 0 || CurrentHp <= 0) return;
        CurrentDs = Math.Min(MaxDs, CurrentDs + amount);
    }

    /// <summary>
    /// Construye y calcula las estadísticas de un Digimon a partir de la plantilla de MongoDB,
    /// su nivel y su escala de tamaño (Size), siguiendo la lógica oficial de DMO / ODMO.
    /// </summary>
    public static DigimonStats FromTemplate(
        MonsterTemplateDocument? template,
        short level,
        short size = 10000,
        int friendship = 20)
    {
        var stats = new DigimonStats
        {
            Level = (byte)Math.Clamp((int)level, 1, 255),
            Friendship = Math.Clamp(friendship, 0, 100)
        };

        if (template == null)
        {
            // Valores por defecto seguros si la especie aún no está registrada en MongoDB
            stats.MaxHp = 620;
            stats.CurrentHp = 620;
            stats.MaxDs = 240;
            stats.CurrentDs = 240;
            stats.Attack = 135;
            stats.Defense = 28;
            stats.AttackSpeed = 2000;
            stats.AttackRange = 250;
            stats.MoveSpeed = 600;
            stats.CriticalRate = 5.0f;
            stats.HitRate = 500;
            stats.EvasionRate = 0.0f;
            stats.BlockRate = 0.0f;
            stats.DigimonExpReward = 50;
            stats.ExpReward = 50;
            return stats;
        }

        // Convertir formato DMO XML (ej: CT 1058 -> 10.58%, EV 1973 -> 19.73%)
        float baseCt = template.Critical > 100 ? (template.Critical / 100.0f) : (float)template.Critical;
        float baseEv = template.Evasion > 100 ? (template.Evasion / 100.0f) : (float)template.Evasion;

        // Para monstruos: los valores en MongoDB son los stats finales del mob
        stats.Level = level > 0 ? (byte)level : (template.Level > 0 ? template.Level : (byte)1);
        stats.MaxHp = template.MaxHp > 0 ? template.MaxHp : 500;
        stats.CurrentHp = stats.MaxHp;
        stats.Attack = template.Attack > 0 ? template.Attack : 100;
        stats.Defense = template.Defense;
        stats.CriticalRate = Math.Max(0f, baseCt);

        int baseDs = template.Ds > 0 ? template.Ds : 200;
        stats.MaxDs = baseDs;
        stats.CurrentDs = stats.MaxDs;

        stats.AttackSpeed = template.AttackSpeed > 0 ? template.AttackSpeed : 2000;
        stats.AttackRange = template.AttackRange > 0 ? template.AttackRange : 250;
        stats.MoveSpeed = template.MoveSpeed > 0 ? template.MoveSpeed : 600;
        stats.HitRate = template.HitRate > 0 ? template.HitRate : 500;
        stats.EvasionRate = baseEv;
        stats.BlockRate = 0.0f;
        stats.DigimonExpReward = template.DigimonExpReward;
        stats.ExpReward = (int)template.DigimonExpReward;

        stats.AttributeType = template.AttributeType > 0 ? template.AttributeType : 1;
        stats.NatureType = template.NatureType > 0 ? template.NatureType : 0;

        // Skills estándar calculadas por modelo de Digimon
        int mobModel = (int)template.MonsterId;
        stats.Skill1 = (mobModel * 100) + 11;
        stats.Skill2 = (mobModel * 100) + 21;

        return stats;
    }

    /// <summary>
    /// Construye y calcula las estadísticas de un Digimon Partner a partir de la plantilla de Digimon_List.xml en MongoDB,
    /// su nivel y su escala de tamaño (Size), siguiendo la lógica oficial de DMO / ODMO.
    /// </summary>
    public static DigimonStats FromDigimonTemplate(
        DigimonTemplateDocument? template,
        short level,
        short size = 10000,
        int friendship = 20)
    {
        var stats = new DigimonStats
        {
            Level = (byte)Math.Clamp((int)level, 1, 255),
            Friendship = Math.Clamp(friendship, 0, 100)
        };

        float sizeMult = size > 0 ? (size / 10000.0f) : 1.0f;
        int lvlBonus = Math.Max(0, level - 1);

        if (template == null)
        {
            // Valores base de Rookie oficial (Agumon 31001) con escalado fiel por nivel
            stats.MaxHp = Math.Max(1, (int)((580 + (lvlBonus * 48)) * sizeMult));
            stats.CurrentHp = stats.MaxHp;
            stats.MaxDs = Math.Max(1, 210 + (lvlBonus * 18));
            stats.CurrentDs = stats.MaxDs;
            stats.Attack = Math.Max(1, (int)((142 + (lvlBonus * 7)) * sizeMult));
            stats.Defense = Math.Max(1, (int)((32 + (lvlBonus * 2)) * sizeMult));
            stats.AttackSpeed = 2000;
            stats.AttackRange = 80;
            stats.MoveSpeed = 600;
            stats.CriticalRate = Math.Max(0f, (5.0f + (lvlBonus * 0.1f)) * sizeMult);
            stats.HitRate = 500 + (lvlBonus * 5);
            stats.EvasionRate = 12.0f;
            stats.BlockRate = 0.0f;
            stats.AttributeType = 3; // Vaccine
            stats.NatureType = 18;   // Fire
            stats.Skill1 = 3100111;
            stats.Skill2 = 3100121;
            return stats;
        }

        // Convertir formato DMO XML (ej: CR 650 -> 6.50%, EV 2230 -> 22.30%)
        float baseCt = template.BaseCritical > 100 ? (template.BaseCritical / 100.0f) : (float)template.BaseCritical;
        float baseEv = template.BaseEvasion > 100 ? (template.BaseEvasion / 100.0f) : (float)template.BaseEvasion;

        // Crecimiento por nivel basado en los stats base de la plantilla oficial de Digimon_List
        int hpGain = Math.Max(12, template.BaseHp / 20);
        int atGain = Math.Max(3, template.BaseAttack / 25);
        int deGain = Math.Max(1, template.BaseDefense / 30);
        int dsGain = Math.Max(2, (template.BaseDs > 0 ? template.BaseDs : 100) / 30);

        // --- STATS QUE ESCALAN CON SIZE Y NIVEL ---
        stats.MaxHp = Math.Max(1, (int)((template.BaseHp + (lvlBonus * hpGain)) * sizeMult));
        stats.CurrentHp = stats.MaxHp;
        stats.Attack = Math.Max(1, (int)((template.BaseAttack + (lvlBonus * atGain)) * sizeMult));
        stats.Defense = Math.Max(1, (int)((template.BaseDefense + (lvlBonus * deGain)) * sizeMult));
        stats.CriticalRate = Math.Max(0f, (baseCt + (lvlBonus * 0.05f)) * sizeMult);

        // --- STATS INDEPENDIENTES DE SIZE ---
        int baseDs = template.BaseDs > 0 ? template.BaseDs : 200;
        stats.MaxDs = Math.Max(1, baseDs + (lvlBonus * dsGain));
        stats.CurrentDs = stats.MaxDs;

        stats.AttackSpeed = template.BaseAttackSpeed > 0 ? template.BaseAttackSpeed : 1800;
        stats.AttackRange = template.BaseAttackRange > 0 ? template.BaseAttackRange : 80;
        stats.MoveSpeed = template.BaseMoveSpeed > 0 ? template.BaseMoveSpeed : 600;
        stats.HitRate = template.BaseHitRate > 0 ? template.BaseHitRate : 500;
        stats.EvasionRate = baseEv;
        stats.BlockRate = 0.0f;

        // Atributo y Elemento reales de la plantilla de Digimon_List.xml
        stats.AttributeType = template.AttributeType > 0 ? template.AttributeType : 3;
        stats.NatureType = template.NatureType > 0 ? template.NatureType : 18;

        // Skills del Digimon (con fallback automático por modelo si la plantilla no los trajo)
        int digiModel = template.Model > 0 ? template.Model : (template.Species > 0 ? template.Species : 31001);
        stats.Skill1 = template.Skill1 > 0 ? template.Skill1 : (digiModel * 100 + 11);
        stats.Skill2 = template.Skill2 > 0 ? template.Skill2 : (digiModel * 100 + 21);
        stats.Skill3 = template.Skill3;
        stats.Skill4 = template.Skill4;

        return stats;
    }
}