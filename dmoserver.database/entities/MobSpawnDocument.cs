namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class MobSpawnDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("map_id")]
    public int MapId { get; set; }

    [BsonElement("monster_id")]
    public int MonsterId { get; set; }

    [BsonElement("x")]
    public int X { get; set; }

    [BsonElement("y")]
    public int Y { get; set; }
}
