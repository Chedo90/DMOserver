namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public sealed class GameAccount
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    [BsonElement("accountId")]
    public uint AccountId { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("characters")]
    public List<CharacterDocument> Characters { get; set; } = [];

    [BsonElement("lastPlayedSlot")]
    public byte LastPlayedSlot { get; set; } = 0;
}

public sealed class CharacterDocument
{
    [BsonElement("slot")]
    public byte Slot { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = "Chedo";

    [BsonElement("model")]
    public int Model { get; set; } = 80001;

    [BsonElement("level")]
    public short Level { get; set; } = 1;

    [BsonElement("location")]
    public CharacterLocation Location { get; set; } = new();

    [BsonElement("partner")]
    public PartnerDigimonDocument Partner { get; set; } = new();
}

public sealed class CharacterLocation
{
    [BsonElement("mapId")]
    public int MapId { get; set; } = 1;

    [BsonElement("x")]
    public int X { get; set; } = 30000;

    [BsonElement("y")]
    public int Y { get; set; } = 30000;

    [BsonElement("z")]
    public float Z { get; set; } = 0.0f;
}

public sealed class PartnerDigimonDocument
{
    [BsonElement("name")]
    public string Name { get; set; } = "Chedorra";

    [BsonElement("model")]
    public int Model { get; set; } = 31001;

    [BsonElement("hatchGrade")]
    public byte HatchGrade { get; set; } = 3;

    [BsonElement("size")]
    public short Size { get; set; } = 10000;

    [BsonElement("level")]
    public short Level { get; set; } = 1;
}