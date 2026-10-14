namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Representa un portal de teletransporte (de suelo o vía NPC) almacenado en MongoDB.
/// Sigue fielmente la estructura de 'MapPortal.xml' y los requerimientos de 'NPC.xml'.
/// Permite añadir nuevos mapas y destinos editando o insertando documentos en la colección 'portals'.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PortalTemplateDocument
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    // ID único del portal (coincide con s_dwPortalID del cliente)
    [BsonElement("portalId")]
    public int PortalId { get; set; }

    // Tipo de portal en el grupo (s_dwPortalType)
    [BsonElement("portalType")]
    public int PortalType { get; set; }

    // Mapa origen (s_dwSrcMapID)
    [BsonElement("srcMapId")]
    public int SrcMapId { get; set; }

    // Coordenadas origen (s_nSrcTargetX, s_nSrcTargetY, s_nSrcRadius)
    [BsonElement("srcX")]
    public int SrcX { get; set; }

    [BsonElement("srcY")]
    public int SrcY { get; set; }

    [BsonElement("srcRadius")]
    public int SrcRadius { get; set; }

    // Mapa destino (s_dwDestMapID)
    [BsonElement("destMapId")]
    public int DestMapId { get; set; }

    // Coordenadas destino (s_nDestTargetX, s_nDestTargetY, s_nDestRadius)
    [BsonElement("destX")]
    public int DestX { get; set; }

    [BsonElement("destY")]
    public int DestY { get; set; }

    [BsonElement("destRadius")]
    public int DestRadius { get; set; }

    /// <summary>
    /// Tipo de portal según s_ePortalType:
    /// 0 = Portal de suelo (Normal walk-in)
    /// 1 = Portal vía diálogo de NPC
    /// </summary>
    [BsonElement("portalKind")]
    public int PortalKind { get; set; }

    /// <summary>
    /// ID del NPC si portalKind == 1 (s_dwUniqObjectID)
    /// </summary>
    [BsonElement("npcId")]
    public int NpcId { get; set; }

    /// <summary>
    /// Índice de la opción de viaje en el menú del NPC (s_nPortalKindIndex)
    /// </summary>
    [BsonElement("portalIndex")]
    public int PortalIndex { get; set; }

    [BsonElement("viewTargetX")]
    public int ViewTargetX { get; set; }

    [BsonElement("viewTargetY")]
    public int ViewTargetY { get; set; }

    // --- Requerimientos opcionales para viajar ---
    [BsonElement("costBits")]
    public long CostBits { get; set; } = 0;

    [BsonElement("requiredItemId")]
    public int RequiredItemId { get; set; } = 0;

    [BsonElement("requiredItemCount")]
    public int RequiredItemCount { get; set; } = 0;

    [BsonElement("requiredTamerLevel")]
    public short RequiredTamerLevel { get; set; } = 0;

    // Nombre descriptivo para facilitar la gestión humana en MongoDB
    [BsonElement("comment")]
    public string? Comment { get; set; }
}
