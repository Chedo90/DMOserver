namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

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

    [BsonElement("sessionToken")]
    [BsonIgnoreIfNull]
    public string? SessionToken { get; set; }

    [BsonElement("characters")]
    public List<CharacterDocument> Characters { get; set; } = [];

    [BsonElement("lastPlayedSlot")]
    public byte LastPlayedSlot { get; set; } = 0;
}

[BsonIgnoreExtraElements]
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

    [BsonElement("bits")]
    public long Bits { get; set; } = 10000;

    [BsonElement("location")]
    public CharacterLocation Location { get; set; } = new();

    [BsonElement("partner")]
    public PartnerDigimonDocument Partner { get; set; } = new();

    [BsonElement("exp")]
    public long Exp { get; set; } = 0;
}

public sealed class CharacterLocation
{
    [BsonElement("mapId")]
    public int MapId { get; set; } = 105;

    [BsonElement("x")]
    public int X { get; set; } = 15016;

    [BsonElement("y")]
    public int Y { get; set; } = 10378;

    [BsonElement("z")]
    public float Z { get; set; } = -2.724508f;
}

[BsonIgnoreExtraElements]
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

    [BsonElement("exp")]
    public long Exp { get; set; } = 0;

    [BsonElement("friendship")]
    public int Friendship { get; set; } = 20;

    [BsonElement("skillPoints")]
    public byte SkillPoints { get; set; } = 2;

    [BsonElement("skillMastery")]
    public byte SkillMastery { get; set; } = 0;

    [BsonElement("skillExperience")]
    public int SkillExperience { get; set; } = 0;

    [BsonElement("skillLevel1")]
    public byte SkillLevel1 { get; set; } = 1;

    [BsonElement("skillLevel2")]
    public byte SkillLevel2 { get; set; } = 1;

    [BsonElement("skillLevel3")]
    public byte SkillLevel3 { get; set; } = 0;

    [BsonElement("skillLevel4")]
    public byte SkillLevel4 { get; set; } = 0;

    [BsonElement("skillLevel5")]
    public byte SkillLevel5 { get; set; } = 0;
}