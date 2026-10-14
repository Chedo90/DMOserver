namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class DigimonTemplateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    // --- Identificadores ---
    [BsonElement("Species")]
    public int Species { get; set; }

    [BsonElement("species")]
    public int SpeciesOld { get => Species; set => Species = value; }

    [BsonElement("Model")]
    public int Model { get; set; }

    [BsonElement("model")]
    public int ModelOld { get => Model; set => Model = value; }

    [BsonElement("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("name")]
    public string NameOld { get => Name; set => Name = value; }

    // --- Tipos y Clasificación ---
    [BsonElement("EvolutionType")]
    public int EvolutionType { get; set; }

    [BsonElement("AttributeType")]
    public int AttributeType { get; set; }

    [BsonElement("Family1")]
    public int Family1 { get; set; }

    [BsonElement("Family2")]
    public int Family2 { get; set; }

    [BsonElement("Family3")]
    public int Family3 { get; set; }

    [BsonElement("NatureType")]
    public int NatureType { get; set; }

    [BsonElement("BaseLevel")]
    public int BaseLevel { get; set; }

    // --- Estadísticas Base de Nivel 1 del Partner ---
    [BsonElement("s_nBaseStat_HP")]
    public int BaseHp { get; set; }

    [BsonElement("HP")]
    public int BaseHpAlias { get => BaseHp; set => BaseHp = value; }

    [BsonElement("max_hp")]
    public int BaseHpOld { get => BaseHp; set => BaseHp = value; }

    [BsonElement("s_nBaseStat_DS")]
    public int BaseDs { get; set; }

    [BsonElement("DS")]
    public int BaseDsAlias { get => BaseDs; set => BaseDs = value; }

    [BsonElement("ds")]
    public int BaseDsOld { get => BaseDs; set => BaseDs = value; }

    [BsonElement("s_nBaseStat_DE")]
    public int BaseDefense { get; set; }

    [BsonElement("DE")]
    public int BaseDefenseAlias { get => BaseDefense; set => BaseDefense = value; }

    [BsonElement("defense")]
    public int BaseDefenseOld { get => BaseDefense; set => BaseDefense = value; }

    [BsonElement("s_nBaseStat_AT")]
    public int BaseAttack { get; set; }

    [BsonElement("AT")]
    public int BaseAttackAlias { get => BaseAttack; set => BaseAttack = value; }

    [BsonElement("attack")]
    public int BaseAttackOld { get => BaseAttack; set => BaseAttack = value; }

    [BsonElement("s_nBaseStat_EV")]
    public int BaseEvasion { get; set; }

    [BsonElement("EV")]
    public int BaseEvasionAlias { get => BaseEvasion; set => BaseEvasion = value; }

    [BsonElement("evasion")]
    public int BaseEvasionOld { get => BaseEvasion; set => BaseEvasion = value; }

    [BsonElement("s_nBaseStat_CR")]
    public int BaseCritical { get; set; }

    [BsonElement("CR")]
    public int BaseCriticalAlias { get => BaseCritical; set => BaseCritical = value; }

    [BsonElement("CT")]
    public int BaseCriticalCt { get => BaseCritical; set => BaseCritical = value; }

    [BsonElement("critical")]
    public int BaseCriticalOld { get => BaseCritical; set => BaseCritical = value; }

    [BsonElement("s_nBaseStat_HT")]
    public int BaseHitRate { get; set; }

    [BsonElement("HT")]
    public int BaseHitRateAlias { get => BaseHitRate; set => BaseHitRate = value; }

    [BsonElement("hit_rate")]
    public int BaseHitRateOld { get => BaseHitRate; set => BaseHitRate = value; }

    [BsonElement("s_nBaseStat_AS")]
    public int BaseAttackSpeed { get; set; }

    [BsonElement("AS")]
    public int BaseAttackSpeedAlias { get => BaseAttackSpeed; set => BaseAttackSpeed = value; }

    [BsonElement("attack_speed")]
    public int BaseAttackSpeedOld { get => BaseAttackSpeed; set => BaseAttackSpeed = value; }

    [BsonElement("s_nBaseStat_AR")]
    public int BaseAttackRange { get; set; }

    [BsonElement("AR")]
    public int BaseAttackRangeAlias { get => BaseAttackRange; set => BaseAttackRange = value; }

    [BsonElement("attack_range")]
    public int BaseAttackRangeOld { get => BaseAttackRange; set => BaseAttackRange = value; }

    [BsonElement("s_nBaseStat_MS")]
    public int BaseMoveSpeed { get; set; }

    [BsonElement("MS")]
    public int BaseMoveSpeedAlias { get => BaseMoveSpeed; set => BaseMoveSpeed = value; }

    [BsonElement("move_speed")]
    public int BaseMoveSpeedOld { get => BaseMoveSpeed; set => BaseMoveSpeed = value; }

    // --- Habilidades / Skills ---
    [BsonElement("Skill1_ID")]
    public int Skill1 { get; set; }

    [BsonElement("Skill2_ID")]
    public int Skill2 { get; set; }

    [BsonElement("Skill3_ID")]
    public int Skill3 { get; set; }

    [BsonElement("Skill4_ID")]
    public int Skill4 { get; set; }

    // --- Forma / Grupo ---
    [BsonElement("Form")]
    public string Form { get; set; } = string.Empty;
}
