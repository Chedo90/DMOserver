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

    [BsonElement("max_hp")]
    public int MaxHp { get; set; }

    [BsonElement("attack")]
    public int Attack { get; set; }

    [BsonElement("defense")]
    public int Defense { get; set; }

    [BsonElement("exp_reward")]
    public int ExpReward { get; set; }
}