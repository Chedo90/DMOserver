namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class MonsterTemplateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("monster_id")]
    public uint MonsterId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("level")]
    public byte Level { get; set; } = 1;

    // --- Vida y Recursos ---
    [BsonElement("max_hp")]
    public int MaxHp { get; set; }

    [BsonElement("ds")]
    public int Ds { get; set; }

    // --- Estadísticas de Combate ---
    [BsonElement("attack")]
    public int Attack { get; set; }

    [BsonElement("defense")]
    public int Defense { get; set; }

    [BsonElement("evasion")]
    public int Evasion { get; set; }

    [BsonElement("critical")]
    public int Critical { get; set; }

    [BsonElement("hit_rate")]
    public int HitRate { get; set; }

    [BsonElement("attack_speed")]
    public int AttackSpeed { get; set; }

    [BsonElement("attack_range")]
    public int AttackRange { get; set; }

    // --- Movimiento e IA ---
    [BsonElement("move_speed")]
    public int MoveSpeed { get; set; }

    [BsonElement("walk_speed")]
    public int WalkSpeed { get; set; }

    [BsonElement("sight")]
    public int Sight { get; set; }

    [BsonElement("hunt_range")]
    public int HuntRange { get; set; }

    // --- Clasificación y Comportamiento ---
    [BsonElement("class")]
    public byte Class { get; set; }

    [BsonElement("battle")]
    public byte Battle { get; set; }

    // --- Recompensa de Experiencia ---
    [BsonElement("DigimonExpReward")]
    public long DigimonExpReward { get; set; }
}