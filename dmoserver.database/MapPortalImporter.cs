namespace dmoserver.database;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using MongoDB.Driver;

public static class MapPortalImporter
{
    private static readonly string[] PossibleMapPortalXmlPaths = new[]
    {
        @"C:\Users\unive\Desktop\Decopilador\bin\Debug\net10.0-windows\TestOutput\XML\MapPortal.xml",
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XML", "MapPortal.xml"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MapPortal.xml")
    };

    private static readonly string[] PossibleNpcXmlPaths = new[]
    {
        @"C:\Users\unive\Desktop\Decopilador\bin\Debug\net10.0-windows\TestOutput\XML\NPC.xml",
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XML", "NPC.xml"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NPC.xml")
    };

    /// <summary>
    /// Comprueba si la colección 'portals' en MongoDB está poblada.
    /// Si está vacía, lee 'MapPortal.xml' y los requerimientos de 'NPC.xml'
    /// e inserta todos los portales en MongoDB siguiendo la estructura oficial.
    /// Esto permite a los administradores crear o modificar mapas y portales desde MongoDB.
    /// </summary>
    public static async Task EnsurePortalsAsync(MongoDbContext db)
    {
        try
        {
            long existingCount = await db.Portals.CountDocumentsAsync(FilterDefinition<PortalTemplateDocument>.Empty);
            if (existingCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[MongoDB] Colección 'portals' cargada ({existingCount} portales registrados).");
                Console.ResetColor();
                return;
            }

            string? portalXmlPath = null;
            foreach (var p in PossibleMapPortalXmlPaths)
            {
                if (File.Exists(p))
                {
                    portalXmlPath = p;
                    break;
                }
            }

            if (portalXmlPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[DB Importer] AVISO: No se encontró 'MapPortal.xml' para poblar 'portals'.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DB Importer] Inicializando colección 'portals' en MongoDB desde: {portalXmlPath}...");
            Console.ResetColor();

            // 1. Cargar requisitos de viaje de los NPCs desde NPC.xml si existe
            var npcRequirements = LoadNpcPortalRequirements();

            // 2. Cargar MapPortal.xml
            var portalDoc = XDocument.Load(portalXmlPath);
            var portalInfos = portalDoc.Descendants("PortalInfo");
            var documents = new List<PortalTemplateDocument>();

            foreach (var p in portalInfos)
            {
                int portalId = ParseInt(p.Element("s_dwPortalID")?.Value);
                if (portalId <= 0) continue;

                int portalType = ParseInt(p.Element("s_dwPortalType")?.Value);
                int srcMapId = ParseInt(p.Element("s_dwSrcMapID")?.Value);
                int srcX = ParseInt(p.Element("s_nSrcTargetX")?.Value);
                int srcY = ParseInt(p.Element("s_nSrcTargetY")?.Value);
                int srcRadius = ParseInt(p.Element("s_nSrcRadius")?.Value);
                int destMapId = ParseInt(p.Element("s_dwDestMapID")?.Value);
                int destX = ParseInt(p.Element("s_nDestTargetX")?.Value);
                int destY = ParseInt(p.Element("s_nDestTargetY")?.Value);
                int destRadius = ParseInt(p.Element("s_nDestRadius")?.Value);
                int portalKind = ParseInt(p.Element("s_ePortalType")?.Value); // 0 = Suelo, 1 = NPC
                int npcId = ParseInt(p.Element("s_dwUniqObjectID")?.Value);
                int portalIndex = ParseInt(p.Element("s_nPortalKindIndex")?.Value);
                int viewTargetX = ParseInt(p.Element("s_nViewTargetX")?.Value);
                int viewTargetY = ParseInt(p.Element("s_nViewTargetY")?.Value);

                long costBits = 0;
                int reqItemId = 0;
                int reqItemCount = 0;

                if (portalKind == 1 && npcId > 0)
                {
                    if (npcRequirements.TryGetValue((npcId, portalIndex), out var req))
                    {
                        costBits = req.costBits;
                        reqItemId = req.itemId;
                        reqItemCount = req.itemCount;
                    }
                }

                documents.Add(new PortalTemplateDocument
                {
                    PortalId = portalId,
                    PortalType = portalType,
                    SrcMapId = srcMapId,
                    SrcX = srcX,
                    SrcY = srcY,
                    SrcRadius = srcRadius,
                    DestMapId = destMapId,
                    DestX = destX,
                    DestY = destY,
                    DestRadius = destRadius,
                    PortalKind = portalKind,
                    NpcId = npcId,
                    PortalIndex = portalIndex,
                    ViewTargetX = viewTargetX,
                    ViewTargetY = viewTargetY,
                    CostBits = costBits,
                    RequiredItemId = reqItemId,
                    RequiredItemCount = reqItemCount
                });
            }

            if (documents.Count > 0)
            {
                await db.Portals.InsertManyAsync(documents);
                await db.Portals.Indexes.CreateOneAsync(
                    new CreateIndexModel<PortalTemplateDocument>(
                        Builders<PortalTemplateDocument>.IndexKeys.Ascending(x => x.PortalId),
                        new CreateIndexOptions { Unique = true }));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[DB Importer] ¡Colección 'portals' poblada con éxito! ({documents.Count} portales importados).");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[DB Importer] Error inicializando portales en MongoDB: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static Dictionary<(int npcId, int portalIndex), (long costBits, int itemId, int itemCount)> LoadNpcPortalRequirements()
    {
        var result = new Dictionary<(int npcId, int portalIndex), (long costBits, int itemId, int itemCount)>();

        try
        {
            string? npcXmlPath = null;
            foreach (var p in PossibleNpcXmlPaths)
            {
                if (File.Exists(p))
                {
                    npcXmlPath = p;
                    break;
                }
            }

            if (npcXmlPath == null) return result;

            var npcDoc = XDocument.Load(npcXmlPath);
            var npcNodes = npcDoc.Descendants("NPC");

            foreach (var npc in npcNodes)
            {
                int npcId = ParseInt(npc.Element("NpcID")?.Value);
                if (npcId <= 0) continue;

                var portalsNode = npc.Element("Portals");
                if (portalsNode == null) continue;

                var portalTypeNodes = portalsNode.Descendants("PortalType");
                int index = 0;

                foreach (var pt in portalTypeNodes)
                {
                    long costBits = 0;
                    int reqItemId = 0;
                    int reqItemCount = 0;

                    var reqItems = pt.Descendants("ReqItem");
                    foreach (var item in reqItems)
                    {
                        int enableType = ParseInt(item.Element("s_eEnableType")?.Value);
                        int enableId = ParseInt(item.Element("s_nEnableID")?.Value);
                        int enableCount = ParseInt(item.Element("s_nEnableCount")?.Value);

                        if (enableType == 4) // Bits
                        {
                            costBits = enableId;
                        }
                        else if (enableType == 1) // Item
                        {
                            reqItemId = enableId;
                            reqItemCount = enableCount > 0 ? enableCount : 1;
                        }
                    }

                    if (costBits > 0 || reqItemId > 0)
                    {
                        result[(npcId, index)] = (costBits, reqItemId, reqItemCount);
                    }

                    index++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[DB Importer] Aviso al leer requisitos de NPC.xml: {ex.Message}");
            Console.ResetColor();
        }

        return result;
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        return int.TryParse(value, out int v) ? v : 0;
    }
}
