namespace dmoserver.database;

using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class MobTemplateDocument
{
    [BsonId]
    public long Id { get; set; }

    public int Type { get; set; }
    public int Model { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Level { get; set; } = 1;

    // Rango e IA
    public int ViewRange { get; set; } = 350;
    public int HuntRange { get; set; } = 4000;
    public int Class { get; set; }
    public int RespawnInterval { get; set; } = 5;

    // Parámetros de Coliseo
    public bool Coliseum { get; set; }
    public byte Round { get; set; }

    // Tipos, Atributos y Familias (guardados como enteros para no depender de enums externos)
    public int ReactionType { get; set; }
    public int Attribute { get; set; }
    public int Element { get; set; }
    public int Family1 { get; set; }
    public int Family2 { get; set; }
    public int Family3 { get; set; }

    // Estadísticas de combate
    public int ASValue { get; set; } = 2500; // Attack Speed
    public int ARValue { get; set; } = 150;  // Attack Range
    public int ATValue { get; set; }         // Attack
    public int BLValue { get; set; }         // Block
    public int CTValue { get; set; }         // Critical
    public int DEValue { get; set; }         // Defense
    public int DSValue { get; set; }         // DigiSoul
    public int EVValue { get; set; }         // Evasion
    public int HPValue { get; set; } = 1000; // Health
    public int HTValue { get; set; }         // Hit Rate
    public int MSValue { get; set; } = 650;  // Run Speed
    public int WSValue { get; set; } = 350;  // Walk Speed

    // Recompensas
    public MobDropRewardData DropReward { get; set; } = new();
    public MobExpRewardData ExpReward { get; set; } = new();
}

public class MobDropRewardData
{
    public byte MinAmount { get; set; }
    public byte MaxAmount { get; set; } = 1;
    public List<MobItemDropData> Drops { get; set; } = [];
    public MobBitDropData BitsDrop { get; set; } = new();
}

public class MobBitDropData
{
    public int MinAmount { get; set; }
    public int MaxAmount { get; set; } = 150;
    public double Chance { get; set; } = 87.5;
}

public class MobItemDropData
{
    public int ItemId { get; set; }
    public int MinAmount { get; set; } = 1;
    public int MaxAmount { get; set; } = 1;
    public double Chance { get; set; }
    public int Rank { get; set; } = 1;
}

public class MobExpRewardData
{
    public long TamerExperience { get; set; }
    public long DigimonExperience { get; set; }
    public short NatureExperience { get; set; }
    public short ElementExperience { get; set; }
    public short SkillExperience { get; set; }
}