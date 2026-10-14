namespace dmoserver.database;

using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class MonsterTemplateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    // --- ID del Monstruo (soporta uint, int, long o string en MongoDB) ---
    [BsonElement("MonsterID")]
    public BsonValue? RawMonsterId { get; set; }

    [BsonElement("monster_id")]
    public BsonValue? RawMonsterIdOld { get; set; }

    [BsonElement("MonsterId")]
    public BsonValue? RawMonsterIdPascal { get; set; }

    [BsonElement("s_dwID")]
    public BsonValue? RawSDwId { get; set; }

    [BsonElement("s_dwMonsterID")]
    public BsonValue? RawSDwMonsterId { get; set; }

    [BsonElement("dwMonsterID")]
    public BsonValue? RawDwMonsterId { get; set; }

    [BsonElement("id")]
    public BsonValue? RawIdLower { get; set; }

    [BsonIgnore]
    public uint MonsterId
    {
        get
        {
            uint id = ParseUInt(RawMonsterId);
            if (id > 0) return id;
            id = ParseUInt(RawMonsterIdOld);
            if (id > 0) return id;
            id = ParseUInt(RawMonsterIdPascal);
            if (id > 0) return id;
            id = ParseUInt(RawSDwId);
            if (id > 0) return id;
            id = ParseUInt(RawSDwMonsterId);
            if (id > 0) return id;
            id = ParseUInt(RawDwMonsterId);
            if (id > 0) return id;
            return ParseUInt(RawIdLower);
        }
        set => RawMonsterId = (int)value;
    }

    // --- Nombre ---
    [BsonElement("Name")]
    public BsonValue? RawName { get; set; }

    [BsonElement("name")]
    public BsonValue? RawNameOld { get; set; }

    [BsonIgnore]
    public string Name
    {
        get
        {
            if (RawName != null && !RawName.IsBsonNull) return RawName.AsString;
            if (RawNameOld != null && !RawNameOld.IsBsonNull) return RawNameOld.AsString;
            return string.Empty;
        }
        set => RawName = value;
    }

    // --- Modelo 3D / ID Visual ---
    [BsonElement("ModelDigimon")]
    public BsonValue? RawModelDigimon { get; set; }

    [BsonElement("model_digimon")]
    public BsonValue? RawModelDigimonOld { get; set; }

    [BsonElement("Model")]
    public BsonValue? RawModel { get; set; }

    [BsonIgnore]
    public int ModelDigimon
    {
        get
        {
            int m = ParseInt(RawModelDigimon);
            if (m > 0) return m;
            m = ParseInt(RawModelDigimonOld);
            if (m > 0) return m;
            return ParseInt(RawModel);
        }
        set => RawModelDigimon = value;
    }

    // --- Nivel ---
    [BsonElement("Level")]
    public BsonValue? RawLevel { get; set; }

    [BsonElement("level")]
    public BsonValue? RawLevelOld { get; set; }

    [BsonIgnore]
    public byte Level
    {
        get
        {
            int l = ParseInt(RawLevel);
            if (l > 0) return (byte)l;
            l = ParseInt(RawLevelOld);
            return (byte)(l > 0 ? l : 1);
        }
        set => RawLevel = value;
    }

    // --- Vida y Recursos (HP / DS) ---
    [BsonElement("HP")]
    public BsonValue? RawHp { get; set; }

    [BsonElement("max_hp")]
    public BsonValue? RawMaxHp { get; set; }

    [BsonElement("MaxHP")]
    public BsonValue? RawMaxHpPascal { get; set; }

    [BsonElement("hp")]
    public BsonValue? RawHpLower { get; set; }

    [BsonElement("s_nHP")]
    public BsonValue? RawSnHp { get; set; }

    [BsonElement("s_dwMaxHP")]
    public BsonValue? RawSDwMaxHp { get; set; }

    [BsonIgnore]
    public int MaxHp
    {
        get
        {
            int hp = ParseInt(RawHp);
            if (hp > 0) return hp;
            hp = ParseInt(RawMaxHp);
            if (hp > 0) return hp;
            hp = ParseInt(RawMaxHpPascal);
            if (hp > 0) return hp;
            hp = ParseInt(RawHpLower);
            if (hp > 0) return hp;
            hp = ParseInt(RawSnHp);
            if (hp > 0) return hp;
            return ParseInt(RawSDwMaxHp);
        }
        set => RawHp = value;
    }

    [BsonElement("DS")]
    public BsonValue? RawDs { get; set; }

    [BsonElement("ds")]
    public BsonValue? RawDsOld { get; set; }

    [BsonIgnore]
    public int Ds
    {
        get
        {
            int ds = ParseInt(RawDs);
            if (ds > 0) return ds;
            return ParseInt(RawDsOld);
        }
        set => RawDs = value;
    }

    // --- Estadísticas de Combate (AT / DE / EV / CT / HT / AS / AR) ---
    [BsonElement("AT")]
    public BsonValue? RawAttack { get; set; }

    [BsonElement("attack")]
    public BsonValue? RawAttackOld { get; set; }

    [BsonIgnore]
    public int Attack
    {
        get
        {
            int at = ParseInt(RawAttack);
            if (at > 0) return at;
            return ParseInt(RawAttackOld);
        }
        set => RawAttack = value;
    }

    [BsonElement("DE")]
    public BsonValue? RawDefense { get; set; }

    [BsonElement("defense")]
    public BsonValue? RawDefenseOld { get; set; }

    [BsonIgnore]
    public int Defense
    {
        get
        {
            int de = ParseInt(RawDefense);
            if (de > 0) return de;
            return ParseInt(RawDefenseOld);
        }
        set => RawDefense = value;
    }

    [BsonElement("EV")]
    public BsonValue? RawEvasion { get; set; }

    [BsonElement("evasion")]
    public BsonValue? RawEvasionOld { get; set; }

    [BsonIgnore]
    public int Evasion
    {
        get
        {
            int ev = ParseInt(RawEvasion);
            if (ev > 0) return ev;
            return ParseInt(RawEvasionOld);
        }
        set => RawEvasion = value;
    }

    [BsonElement("CT")]
    public BsonValue? RawCritical { get; set; }

    [BsonElement("critical")]
    public BsonValue? RawCriticalOld { get; set; }

    [BsonIgnore]
    public int Critical
    {
        get
        {
            int ct = ParseInt(RawCritical);
            if (ct > 0) return ct;
            return ParseInt(RawCriticalOld);
        }
        set => RawCritical = value;
    }

    [BsonElement("HT")]
    public BsonValue? RawHitRate { get; set; }

    [BsonElement("hit_rate")]
    public BsonValue? RawHitRateOld { get; set; }

    [BsonIgnore]
    public int HitRate
    {
        get
        {
            int ht = ParseInt(RawHitRate);
            if (ht > 0) return ht;
            return ParseInt(RawHitRateOld);
        }
        set => RawHitRate = value;
    }

    [BsonElement("AS")]
    public BsonValue? RawAttackSpeed { get; set; }

    [BsonElement("attack_speed")]
    public BsonValue? RawAttackSpeedOld { get; set; }

    [BsonIgnore]
    public int AttackSpeed
    {
        get
        {
            int as_ = ParseInt(RawAttackSpeed);
            if (as_ > 0) return as_;
            return ParseInt(RawAttackSpeedOld);
        }
        set => RawAttackSpeed = value;
    }

    [BsonElement("AR")]
    public BsonValue? RawAttackRange { get; set; }

    [BsonElement("attack_range")]
    public BsonValue? RawAttackRangeOld { get; set; }

    [BsonIgnore]
    public int AttackRange
    {
        get
        {
            int ar = ParseInt(RawAttackRange);
            if (ar > 0) return ar;
            return ParseInt(RawAttackRangeOld);
        }
        set => RawAttackRange = value;
    }

    // --- Movimiento e IA (MS / WS / Sight / HuntRange) ---
    [BsonElement("MS")]
    public BsonValue? RawMoveSpeed { get; set; }

    [BsonElement("move_speed")]
    public BsonValue? RawMoveSpeedOld { get; set; }

    [BsonIgnore]
    public int MoveSpeed
    {
        get
        {
            int ms = ParseInt(RawMoveSpeed);
            if (ms > 0) return ms;
            return ParseInt(RawMoveSpeedOld);
        }
        set => RawMoveSpeed = value;
    }

    [BsonElement("WS")]
    public BsonValue? RawWalkSpeed { get; set; }

    [BsonElement("walk_speed")]
    public BsonValue? RawWalkSpeedOld { get; set; }

    [BsonIgnore]
    public int WalkSpeed
    {
        get
        {
            int ws = ParseInt(RawWalkSpeed);
            if (ws > 0) return ws;
            return ParseInt(RawWalkSpeedOld);
        }
        set => RawWalkSpeed = value;
    }

    [BsonElement("Sight")]
    public BsonValue? RawSight { get; set; }

    [BsonElement("sight")]
    public BsonValue? RawSightOld { get; set; }

    [BsonIgnore]
    public int Sight
    {
        get
        {
            int s = ParseInt(RawSight);
            if (s > 0) return s;
            return ParseInt(RawSightOld);
        }
        set => RawSight = value;
    }

    [BsonElement("HuntRange")]
    public BsonValue? RawHuntRange { get; set; }

    [BsonElement("hunt_range")]
    public BsonValue? RawHuntRangeOld { get; set; }

    [BsonIgnore]
    public int HuntRange
    {
        get
        {
            int hr = ParseInt(RawHuntRange);
            if (hr > 0) return hr;
            return ParseInt(RawHuntRangeOld);
        }
        set => RawHuntRange = value;
    }

    // --- Clasificación y Comportamiento (Class / Battle) ---
    [BsonElement("Class")]
    public BsonValue? RawClass { get; set; }

    [BsonElement("class")]
    public BsonValue? RawClassOld { get; set; }

    [BsonIgnore]
    public byte Class
    {
        get
        {
            int c = ParseInt(RawClass);
            if (c > 0) return (byte)c;
            return (byte)ParseInt(RawClassOld);
        }
        set => RawClass = value;
    }

    [BsonElement("Battle")]
    public BsonValue? RawBattle { get; set; }

    [BsonElement("battle")]
    public BsonValue? RawBattleOld { get; set; }

    [BsonIgnore]
    public byte Battle
    {
        get
        {
            int b = ParseInt(RawBattle);
            if (b > 0) return (byte)b;
            return (byte)ParseInt(RawBattleOld);
        }
        set => RawBattle = value;
    }

    // --- Atributo y Elemento (AttributeType / NatureType / Icon1 / Icon2) ---
    [BsonElement("AttributeType")]
    public BsonValue? RawAttributeType { get; set; }

    [BsonElement("attribute_type")]
    public BsonValue? RawAttributeTypeLower { get; set; }

    [BsonElement("Icon1")]
    public BsonValue? RawIcon1 { get; set; }

    [BsonIgnore]
    public int AttributeType
    {
        get
        {
            int att = ParseInt(RawAttributeType);
            if (att > 0) return att;
            att = ParseInt(RawAttributeTypeLower);
            if (att > 0) return att;
            int icon1 = ParseInt(RawIcon1);
            return icon1 switch
            {
                11 => 2, // Data
                22 => 3, // Vaccine
                33 => 4, // Virus
                44 => 5, // Unknown
                _ => icon1 > 0 && icon1 <= 5 ? icon1 : 1 // None
            };
        }
        set => RawAttributeType = value;
    }

    [BsonElement("NatureType")]
    public BsonValue? RawNatureType { get; set; }

    [BsonElement("nature_type")]
    public BsonValue? RawNatureTypeLower { get; set; }

    [BsonElement("Icon2")]
    public BsonValue? RawIcon2 { get; set; }

    [BsonIgnore]
    public int NatureType
    {
        get
        {
            int nat = ParseInt(RawNatureType);
            if (nat > 0) return nat;
            nat = ParseInt(RawNatureTypeLower);
            if (nat > 0) return nat;
            return ParseInt(RawIcon2);
        }
        set => RawNatureType = value;
    }

    // --- Experiencia (EXP / DigimonExpReward / ExpMin / ExpMax) ---
    [BsonElement("EXP")]
    public BsonValue? RawExp { get; set; }

    [BsonElement("Exp")]
    public BsonValue? RawExpCapital { get; set; }

    [BsonElement("exp")]
    public BsonValue? RawExpLower { get; set; }

    [BsonElement("DigimonExpReward")]
    public BsonValue? RawDigimonExpReward { get; set; }

    [BsonElement("ExpReward")]
    public BsonValue? RawExpReward { get; set; }

    [BsonElement("exp_reward")]
    public BsonValue? RawExpRewardLower { get; set; }

    [BsonElement("ExpMin")]
    public BsonValue? RawExpMin { get; set; }

    [BsonElement("ExpMax")]
    public BsonValue? RawExpMax { get; set; }

    [BsonElement("Experience")]
    public BsonValue? RawExperience { get; set; }

    [BsonElement("experience")]
    public BsonValue? RawExperienceLower { get; set; }

    [BsonElement("exp_min")]
    public BsonValue? RawExpMinSnake { get; set; }

    [BsonElement("exp_max")]
    public BsonValue? RawExpMaxSnake { get; set; }

    [BsonElement("s_dwEXP")]
    public BsonValue? RawSDwExp { get; set; }

    [BsonElement("dwEXP")]
    public BsonValue? RawDwExp { get; set; }

    [BsonIgnore]
    public long DigimonExpReward
    {
        get
        {
            long exp = ParseLong(RawExp);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpCapital);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpLower);
            if (exp > 0) return exp;
            exp = ParseLong(RawDigimonExpReward);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpReward);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpRewardLower);
            if (exp > 0) return exp;
            exp = ParseLong(RawExperience);
            if (exp > 0) return exp;
            exp = ParseLong(RawExperienceLower);
            if (exp > 0) return exp;
            exp = ParseLong(RawSDwExp);
            if (exp > 0) return exp;
            exp = ParseLong(RawDwExp);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpMin);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpMax);
            if (exp > 0) return exp;
            exp = ParseLong(RawExpMinSnake);
            if (exp > 0) return exp;
            return ParseLong(RawExpMaxSnake);
        }
        set => RawExp = value;
    }

    // --- Métodos de conversión seguros ---
    public static uint ParseUInt(BsonValue? value)
    {
        if (value == null || value.IsBsonNull) return 0;
        if (value.IsInt32) return (uint)value.AsInt32;
        if (value.IsInt64) return (uint)value.AsInt64;
        if (value.IsDouble) return (uint)value.AsDouble;
        if (value.IsString && uint.TryParse(value.AsString.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out uint result))
            return result;
        return 0;
    }

    public static int ParseInt(BsonValue? value)
    {
        if (value == null || value.IsBsonNull) return 0;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return (int)value.AsInt64;
        if (value.IsDouble) return (int)value.AsDouble;
        if (value.IsString && int.TryParse(value.AsString.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
            return result;
        return 0;
    }

    public static long ParseLong(BsonValue? value)
    {
        if (value == null || value.IsBsonNull) return 0;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsDouble) return (long)value.AsDouble;
        if (value.IsString && long.TryParse(value.AsString.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out long result))
            return result;
        return 0;
    }
}