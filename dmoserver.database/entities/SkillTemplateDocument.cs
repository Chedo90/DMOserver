namespace dmoserver.database;

using System;
using System.Collections.Generic;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class SkillApplyDocument
{
    [BsonElement("s_nA")]
    public BsonValue? RawA { get; set; }

    [BsonElement("s_nInvoke_Rate")]
    public BsonValue? RawInvokeRate { get; set; }

    [BsonElement("s_nB")]
    public BsonValue? RawB { get; set; }

    [BsonElement("s_nC")]
    public BsonValue? RawC { get; set; }

    [BsonElement("s_nBuffCode")]
    public BsonValue? RawBuffCode { get; set; }

    [BsonElement("s_nID")]
    public BsonValue? RawId { get; set; }

    [BsonElement("s_nIncrease_B_Point")]
    public BsonValue? RawIncreaseBPoint { get; set; }

    public int s_nA => SkillTemplateDocument.ParseInt(RawA);
    public int s_nInvoke_Rate => SkillTemplateDocument.ParseInt(RawInvokeRate);
    public int s_nB => SkillTemplateDocument.ParseInt(RawB);
    public int s_nC => SkillTemplateDocument.ParseInt(RawC);
    public int s_nBuffCode => SkillTemplateDocument.ParseInt(RawBuffCode);
    public int s_nID => SkillTemplateDocument.ParseInt(RawId);
    public int s_nIncrease_B_Point => SkillTemplateDocument.ParseInt(RawIncreaseBPoint);
}

[BsonIgnoreExtraElements]
public class SkillTemplateDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    // --- ID de la Habilidad (soporta int o string en MongoDB) ---
    [BsonElement("s_dwID")]
    public BsonValue? RawSkillId { get; set; }

    public int SkillId
    {
        get => ParseInt(RawSkillId);
        set => RawSkillId = value;
    }

    [BsonElement("s_szName")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("s_szComment")]
    public string Comment { get; set; } = string.Empty;

    // --- SkillApply (soporta tanto array de objetos como texto plano con espacios/saltos de línea) ---
    [BsonElement("SkillApply")]
    public BsonValue? RawSkillApply { get; set; }

    [BsonElement("s_nLevelupPoint")]
    public BsonValue? RawLevelupPoint { get; set; }

    [BsonElement("s_nMaxLevel")]
    public BsonValue? RawMaxLevel { get; set; }

    [BsonElement("s_nAttributeType")]
    public BsonValue? RawAttributeType { get; set; }

    [BsonElement("s_nNatureType")]
    public BsonValue? RawNatureType { get; set; }

    [BsonElement("s_nFamilyType")]
    public BsonValue? RawFamilyType { get; set; }

    [BsonElement("s_nUseHP")]
    public BsonValue? RawUseHp { get; set; }

    [BsonElement("s_nUseDS")]
    public BsonValue? RawUseDs { get; set; }

    [BsonElement("s_nIcon")]
    public BsonValue? RawIcon { get; set; }

    [BsonElement("s_nTarget")]
    public BsonValue? RawTarget { get; set; }

    [BsonElement("s_nAttType")]
    public BsonValue? RawAttType { get; set; }

    [BsonElement("s_fAttRange")]
    public BsonValue? RawAttRange { get; set; }

    [BsonElement("s_fCastingTime")]
    public BsonValue? RawCastingTime { get; set; }

    [BsonElement("s_fCooldownTime")]
    public BsonValue? RawCooldownTime { get; set; }

    [BsonElement("s_nSkillType")]
    public BsonValue? RawSkillType { get; set; }

    // --- Propiedades tipadas y seguras ---
    public int LevelupPoint => ParseInt(RawLevelupPoint);
    public int MaxLevel => ParseInt(RawMaxLevel, 25);
    public int AttributeType => ParseInt(RawAttributeType);
    public int NatureType => ParseInt(RawNatureType);
    public int FamilyType => ParseInt(RawFamilyType);
    public int UseHp => ParseInt(RawUseHp);
    public int UseDs => ParseInt(RawUseDs);
    public int Icon => ParseInt(RawIcon);
    public int Target => ParseInt(RawTarget);
    public int AttackType => ParseInt(RawAttType);
    public float AttackRange => ParseFloat(RawAttRange);
    public float CastingTime => ParseFloat(RawCastingTime);
    public float CooldownTime => ParseFloat(RawCooldownTime);
    public int SkillType => ParseInt(RawSkillType);

    /// <summary>
    /// Extrae el daño base (s_nB) de SkillApply, ya sea que venga como BsonArray,
    /// BsonDocument o como texto plano con saltos de línea y espacios.
    /// </summary>
    public int BasePower
    {
        get
        {
            if (RawSkillApply == null) return 0;

            // Si es un string (como el importador XML a JSON con espacios/saltos de línea)
            if (RawSkillApply.IsString)
            {
                var tokens = RawSkillApply.AsString.Split(
                    new[] { ' ', '\r', '\n', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                // Cada IncreaseApply tiene 7 valores: [s_nA, s_nInvoke_Rate, s_nB, s_nC, s_nBuffCode, s_nID, s_nIncrease_B_Point]
                // El daño base s_nB es el tercer valor (índice 2)
                if (tokens.Length >= 3 && int.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int bVal))
                {
                    return bVal;
                }
            }
            else if (RawSkillApply.IsBsonArray)
            {
                var arr = RawSkillApply.AsBsonArray;
                if (arr.Count > 0 && arr[0].IsBsonDocument)
                {
                    var doc = arr[0].AsBsonDocument;
                    if (doc.Contains("s_nB")) return ParseInt(doc["s_nB"]);
                }
            }
            else if (RawSkillApply.IsBsonDocument)
            {
                var doc = RawSkillApply.AsBsonDocument;
                if (doc.Contains("s_nB")) return ParseInt(doc["s_nB"]);
                if (doc.Contains("IncreaseApply") && doc["IncreaseApply"].IsBsonDocument)
                {
                    var inner = doc["IncreaseApply"].AsBsonDocument;
                    if (inner.Contains("s_nB")) return ParseInt(inner["s_nB"]);
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// Extrae el incremento de daño por nivel de habilidad (s_nIncrease_B_Point).
    /// </summary>
    public int IncreasePower
    {
        get
        {
            if (RawSkillApply == null) return 0;

            if (RawSkillApply.IsString)
            {
                var tokens = RawSkillApply.AsString.Split(
                    new[] { ' ', '\r', '\n', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                // El incremento s_nIncrease_B_Point es el séptimo valor (índice 6)
                if (tokens.Length >= 7 && int.TryParse(tokens[6], NumberStyles.Any, CultureInfo.InvariantCulture, out int incVal))
                {
                    return incVal;
                }
            }
            else if (RawSkillApply.IsBsonArray)
            {
                var arr = RawSkillApply.AsBsonArray;
                if (arr.Count > 0 && arr[0].IsBsonDocument)
                {
                    var doc = arr[0].AsBsonDocument;
                    if (doc.Contains("s_nIncrease_B_Point")) return ParseInt(doc["s_nIncrease_B_Point"]);
                }
            }
            else if (RawSkillApply.IsBsonDocument)
            {
                var doc = RawSkillApply.AsBsonDocument;
                if (doc.Contains("s_nIncrease_B_Point")) return ParseInt(doc["s_nIncrease_B_Point"]);
            }

            return 0;
        }
    }

    // --- Métodos utilitarios de conversión segura ---
    public static int ParseInt(BsonValue? value, int defaultValue = 0)
    {
        if (value == null || value.IsBsonNull) return defaultValue;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return (int)value.AsInt64;
        if (value.IsDouble) return (int)value.AsDouble;
        if (value.IsString && int.TryParse(value.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out int r))
            return r;
        return defaultValue;
    }

    public static float ParseFloat(BsonValue? value, float defaultValue = 0f)
    {
        if (value == null || value.IsBsonNull) return defaultValue;
        if (value.IsDouble) return (float)value.AsDouble;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsString && float.TryParse(value.AsString, NumberStyles.Any, CultureInfo.InvariantCulture, out float r))
            return r;
        return defaultValue;
    }
}
