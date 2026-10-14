namespace dmoserver.database;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using MongoDB.Driver;

public static class DigimonListImporter
{
    private static readonly string[] PossibleXmlPaths = new[]
    {
        @"C:\Users\unive\Desktop\Decopilador\bin\Debug\net10.0-windows\TestOutput\XML\Digimon_List.xml",
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XML", "Digimon_List.xml"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digimon_List.xml")
    };

    /// <summary>
    /// Comprueba si la colección 'digimon_templates' está poblada. Si está vacía o falta Agumon (31001),
    /// lee 'Digimon_List.xml' e inserta todas las plantillas de Digimons en MongoDB.
    /// </summary>
    public static async Task EnsureDigimonTemplatesAsync(MongoDbContext db)
    {
        try
        {
            var agumon = await db.GetDigimonTemplateAsync(31001);
            if (agumon != null)
            {
                // Ya existe Agumon y la colección está poblada
                return;
            }

            string? xmlPath = null;
            foreach (var p in PossibleXmlPaths)
            {
                if (File.Exists(p))
                {
                    xmlPath = p;
                    break;
                }
            }

            if (xmlPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[DB Importer] AVISO: No se encontró 'Digimon_List.xml' para poblar 'digimon_templates'.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DB Importer] Poblando colección 'digimon_templates' desde: {xmlPath}...");
            Console.ResetColor();

            var xdoc = XDocument.Load(xmlPath);
            var digimonNodes = xdoc.Descendants("Digimon");
            var documents = new List<DigimonTemplateDocument>();

            foreach (var node in digimonNodes)
            {
                int species = int.TryParse(node.Element("Species")?.Value, out int sp) ? sp : 0;
                int model = int.TryParse(node.Element("Model")?.Value, out int md) ? md : species;
                if (species == 0 && model == 0) continue;

                var doc = new DigimonTemplateDocument
                {
                    Species = species,
                    Model = model,
                    DisplayName = node.Element("DisplayName")?.Value ?? string.Empty,
                    Name = node.Element("Name")?.Value ?? string.Empty,
                    EvolutionType = int.TryParse(node.Element("EvolutionType")?.Value, out int evo) ? evo : 0,
                    AttributeType = int.TryParse(node.Element("AttributeType")?.Value, out int att) ? att : 1,
                    Family1 = int.TryParse(node.Element("Family1")?.Value, out int f1) ? f1 : 0,
                    Family2 = int.TryParse(node.Element("Family2")?.Value, out int f2) ? f2 : 0,
                    Family3 = int.TryParse(node.Element("Family3")?.Value, out int f3) ? f3 : 0,
                    NatureType = int.TryParse(node.Element("NatureType")?.Value, out int nat) ? nat : 0,
                    BaseLevel = int.TryParse(node.Element("BaseLevel")?.Value, out int bl) ? bl : 1,
                    BaseHp = int.TryParse(node.Element("s_nBaseStat_HP")?.Value, out int hp) ? hp : 500,
                    BaseDs = int.TryParse(node.Element("s_nBaseStat_DS")?.Value, out int ds) ? ds : 200,
                    BaseDefense = int.TryParse(node.Element("s_nBaseStat_DE")?.Value, out int de) ? de : 20,
                    BaseEvasion = int.TryParse(node.Element("s_nBaseStat_EV")?.Value, out int ev) ? ev : 0,
                    BaseMoveSpeed = int.TryParse(node.Element("s_nBaseStat_MS")?.Value, out int ms) ? ms : 600,
                    BaseCritical = int.TryParse(node.Element("s_nBaseStat_CR")?.Value, out int cr) ? cr : 500,
                    BaseAttack = int.TryParse(node.Element("s_nBaseStat_AT")?.Value, out int at) ? at : 100,
                    BaseAttackSpeed = int.TryParse(node.Element("s_nBaseStat_AS")?.Value, out int as_) ? as_ : 2000,
                    BaseAttackRange = int.TryParse(node.Element("s_nBaseStat_AR")?.Value, out int ar) ? ar : 80,
                    BaseHitRate = int.TryParse(node.Element("s_nBaseStat_HT")?.Value, out int ht) ? ht : 500,
                    Skill1 = int.TryParse(node.Element("Skill1_ID")?.Value, out int sk1) ? sk1 : 0,
                    Skill2 = int.TryParse(node.Element("Skill2_ID")?.Value, out int sk2) ? sk2 : 0,
                    Skill3 = int.TryParse(node.Element("Skill3_ID")?.Value, out int sk3) ? sk3 : 0,
                    Skill4 = int.TryParse(node.Element("Skill4_ID")?.Value, out int sk4) ? sk4 : 0,
                    Form = node.Element("Form")?.Value ?? string.Empty
                };

                documents.Add(doc);
            }

            if (documents.Count > 0)
            {
                await db.DigimonTemplates.InsertManyAsync(documents);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[DB Importer] ¡Se importaron exitosamente {documents.Count} especies de Digimon a 'digimon_templates'!");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[DB Importer] Error al importar 'Digimon_List.xml': {ex.Message}");
            Console.ResetColor();
        }
    }
}
