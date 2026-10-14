namespace dmoserver.database;

using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<GameAccount> _accounts;

    // Colección auxiliar: _id = nombre del tamer en minúsculas. Garantiza unicidad de forma atómica.
    private readonly IMongoCollection<BsonDocument> _tamerNames;

    // Colección auxiliar: contador atómico para AccountId.
    private readonly IMongoCollection<BsonDocument> _counters;

    public MongoDbContext(string connectionString = "mongodb://localhost:27017", string databaseName = "DmoServer")
    {
        var client = new MongoClient(connectionString);

        // Guardamos la base de datos a nivel de clase para que otras colecciones puedan usarla
        _database = client.GetDatabase(databaseName);
        _accounts = _database.GetCollection<GameAccount>("Accounts");
        _tamerNames = _database.GetCollection<BsonDocument>("TamerNames");
        _counters = _database.GetCollection<BsonDocument>("Counters");

        var accountIdIndex = new CreateIndexModel<GameAccount>(
            Builders<GameAccount>.IndexKeys.Ascending(a => a.AccountId),
            new CreateIndexOptions { Unique = true }
        );

        var usernameIndex = new CreateIndexModel<GameAccount>(
            Builders<GameAccount>.IndexKeys.Ascending(a => a.Username),
            new CreateIndexOptions { Unique = true }
        );

        _accounts.Indexes.CreateMany([accountIdIndex, usernameIndex]);

        // El contador de AccountId nunca puede ir por debajo del ID más alto existente.
        var highest = _accounts
            .Find(FilterDefinition<GameAccount>.Empty)
            .SortByDescending(a => a.AccountId)
            .Limit(1)
            .FirstOrDefault();

        _counters.UpdateOne(
            Builders<BsonDocument>.Filter.Eq("_id", "accountId"),
            Builders<BsonDocument>.Update.Max("seq", (long)(highest?.AccountId ?? 0)),
            new UpdateOptions { IsUpsert = true });
    }

    // ==========================================
    // LOGIN SERVER
    // ==========================================

    /// <summary>
    /// Autentica o autoregistra. El nuevo AccountId sale de un contador atómico,
    /// así que dos registros simultáneos nunca colisionan.
    /// </summary>
    public async Task<GameAccount?> AuthenticateOrRegisterAsync(string username, string password)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.Username, username);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var account = await _accounts.Find(filter).FirstOrDefaultAsync();

            if (account != null)
            {
                if (account.PasswordHash == password)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[MongoDB] Login correcto para el usuario: {username}");
                    Console.ResetColor();
                    return account;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[MongoDB] Fallo de login: Contraseña incorrecta para {username}");
                Console.ResetColor();
                return null;
            }

            var newAccount = new GameAccount
            {
                AccountId = await NextAccountIdAsync(),
                Username = username,
                PasswordHash = password,
                Characters = [],
                LastPlayedSlot = 0
            };

            try
            {
                await _accounts.InsertOneAsync(newAccount);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[MongoDB] Cuenta registrada con éxito: {username} (ID: {newAccount.AccountId})");
                Console.ResetColor();
                return newAccount;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Otra conexión registró el mismo usuario (o ID) a la vez: se reintenta y se valida contra la cuenta ya creada.
            }
        }

        return null;
    }

    private async Task<uint> NextAccountIdAsync()
    {
        var counter = await _counters.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "accountId"),
            Builders<BsonDocument>.Update.Inc("seq", 1L),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        return (uint)counter["seq"].ToInt64();
    }

    /// <summary>
    /// Busca la cuenta mediante el token de sesión extraído del paquete DMIPASS
    /// </summary>
    public async Task<GameAccount?> GetAccountBySessionTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var filter = Builders<GameAccount>.Filter.Eq(a => a.SessionToken, token);
        return await _accounts.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Asigna o actualiza el token de sesión generado tras pasar el login
    /// </summary>
    public async Task SetSessionTokenAsync(uint accountId, string token)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId);
        var update = Builders<GameAccount>.Update.Set(a => a.SessionToken, token);
        await _accounts.UpdateOneAsync(filter, update);
    }

    // ==========================================
    // CUENTAS (CHARACTER / GAME SERVER)
    // ==========================================

    public async Task<GameAccount?> GetAccountByIdAsync(uint accountId)
    {
        return await _accounts.Find(a => a.AccountId == accountId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Método requerido por GAME SERVER para obtener la cuenta o crearla si la BD está vacía.
    /// La creación es atómica (upsert): dos llamadas simultáneas no pueden duplicar la cuenta.
    /// </summary>
    public async Task<GameAccount> GetOrCreateAccountAsync(uint accountId)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId);

        var existing = await _accounts.Find(filter).FirstOrDefaultAsync();
        if (existing != null) return existing;

        // Si la base de datos se borró o la cuenta no existe, la inicializamos con datos por defecto
        var update = Builders<GameAccount>.Update
            .SetOnInsert(a => a.Username, $"User_{accountId}")
            .SetOnInsert(a => a.PasswordHash, "admin")
            .SetOnInsert(a => a.LastPlayedSlot, (byte)0)
            .SetOnInsert(a => a.Characters, new List<CharacterDocument>
            {
                new CharacterDocument
                {
                    Slot = 0,
                    Name = "takatt",
                    Model = 80001,
                    Level = 1,
                    Location = new CharacterLocation { MapId = 105, X = 15016, Y = 10378, Z = -2.724508f },
                    Partner = new PartnerDigimonDocument
                    {
                        Name = "Agumon",
                        Model = 31001,
                        Level = 1,
                        Size = 10000,
                        HatchGrade = 3
                    }
                }
            });

        try
        {
            var before = await _accounts.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<GameAccount> { IsUpsert = true, ReturnDocument = ReturnDocument.Before });

            if (before == null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[MongoDB] Cuenta autogenerada: ID {accountId} con Tamer 'takatt' y Partner 'Agumon'");
                Console.ResetColor();
            }
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            // Otra conexión la creó justo a la vez: simplemente la leemos.
        }

        return await _accounts.Find(filter).FirstAsync();
    }

    /// <summary>
    /// Reemplaza la cuenta ENTERA. Es peligroso con varias conexiones a la vez (pisa cambios ajenos).
    /// Se mantiene por compatibilidad con el Game Server; prefiere los métodos atómicos de abajo.
    /// </summary>
    public async Task UpdateAccountAsync(GameAccount account)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, account.AccountId);
        await _accounts.ReplaceOneAsync(filter, account, new ReplaceOptions { IsUpsert = true });
    }

    /// <summary>
    /// Añade un personaje de forma atómica SOLO si la cuenta existe y el slot está libre.
    /// También deja ese slot como último jugado.
    /// </summary>
    /// <returns>true si se añadió; false si el slot estaba ocupado o la cuenta no existe.</returns>
    public async Task<bool> TryAddCharacterAsync(uint accountId, CharacterDocument character)
    {
        byte slot = character.Slot;

        var filter = Builders<GameAccount>.Filter.And(
            Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
            Builders<GameAccount>.Filter.Not(
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot))
        );

        var update = Builders<GameAccount>.Update
            .Push(a => a.Characters, character)
            .Set(a => a.LastPlayedSlot, slot);

        var result = await _accounts.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Marca el slot como último jugado (Update.Set puntual) solo si ese personaje existe.
    /// </summary>
    public async Task<bool> SetLastPlayedSlotAsync(uint accountId, byte slot)
    {
        var filter = Builders<GameAccount>.Filter.And(
            Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
            Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
        );

        var update = Builders<GameAccount>.Update.Set(a => a.LastPlayedSlot, slot);
        var result = await _accounts.UpdateOneAsync(filter, update);
        return result.MatchedCount == 1;
    }

    public async Task UpdatePositionAsync(uint accountId, byte slot, int x, int y, float z)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
            );

            // Operador posicional estricto "$" para arrays en MongoDB
            var update = Builders<GameAccount>.Update
                .Set("characters.$.location.x", x)
                .Set("characters.$.location.y", y)
                .Set("characters.$.location.z", z);

            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error guardando posición: {ex.Message}");
            Console.ResetColor();
        }
    }

    public async Task UpdateLocationAsync(uint accountId, byte slot, int mapId, int x, int y, float z)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
            );

            var update = Builders<GameAccount>.Update
                .Set("characters.$.location.mapId", mapId)
                .Set("characters.$.location.x", x)
                .Set("characters.$.location.y", y)
                .Set("characters.$.location.z", z);

            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error guardando ubicación de mapa: {ex.Message}");
            Console.ResetColor();
        }
    }

    public async Task UpdateBitsAsync(uint accountId, byte slot, long bits)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
            );

            var update = Builders<GameAccount>.Update.Set("characters.$.bits", bits);
            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error guardando bits: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Guarda nivel y experiencia del Tamer y del Digimon partner.
    /// Usa Update.Set puntual (no reemplaza la cuenta entera).
    /// </summary>
    public async Task UpdateProgressAsync(uint accountId, byte slot, short tamerLevel, long tamerExp, short partnerLevel, long partnerExp)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
            );

            // Operador posicional estricto "$" para arrays en MongoDB
            var update = Builders<GameAccount>.Update
                .Set("characters.$.level", tamerLevel)
                .Set("characters.$.exp", tamerExp)
                .Set("characters.$.partner.level", partnerLevel)
                .Set("characters.$.partner.exp", partnerExp);

            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error guardando progreso: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Guarda los puntos de habilidad, maestría, experiencia de skill y niveles de F1-F5 del Digimon partner.
    /// </summary>
    public async Task UpdatePartnerSkillsAsync(
        uint accountId,
        byte slot,
        byte skillPoints,
        byte skillMastery,
        int skillExp,
        byte skillLevel1,
        byte skillLevel2,
        byte skillLevel3,
        byte skillLevel4,
        byte skillLevel5)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.ElemMatch(a => a.Characters, c => c.Slot == slot)
            );

            var update = Builders<GameAccount>.Update
                .Set("characters.$.partner.skillPoints", skillPoints)
                .Set("characters.$.partner.skillMastery", skillMastery)
                .Set("characters.$.partner.skillExperience", skillExp)
                .Set("characters.$.partner.skillLevel1", skillLevel1)
                .Set("characters.$.partner.skillLevel2", skillLevel2)
                .Set("characters.$.partner.skillLevel3", skillLevel3)
                .Set("characters.$.partner.skillLevel4", skillLevel4)
                .Set("characters.$.partner.skillLevel5", skillLevel5);

            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error guardando skills del partner: {ex.Message}");
            Console.ResetColor();
        }
    }

    public async Task UpdatePartnerFriendshipAsync(uint accountId, byte slot, int friendship)
    {
        try
        {
            var filter = Builders<GameAccount>.Filter.And(
                Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
                Builders<GameAccount>.Filter.Eq("characters.slot", slot)
            );

            var update = Builders<GameAccount>.Update.Set(
                "characters.$.partner.friendship", Math.Clamp(friendship, 0, 100));

            await _accounts.UpdateOneAsync(filter, update);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MongoDB] Error actualizando amistad: {ex.Message}");
            Console.ResetColor();
        }
    }

    // ==========================================
    // NOMBRES DE TAMER (únicos, sin importar mayúsculas)
    // ==========================================

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    /// <summary>
    /// Consulta de solo lectura: ¿está el nombre ya reservado por algún personaje?
    /// </summary>
    public async Task<bool> IsTamerNameTakenAsync(string name)
    {
        var count = await _tamerNames.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("_id", NormalizeName(name)),
            new CountOptions { Limit = 1 });

        return count > 0;
    }

    /// <summary>
    /// Reserva el nombre de forma atómica (el _id único evita que dos jugadores lo cojan a la vez).
    /// </summary>
    public async Task<bool> TryReserveTamerNameAsync(string name, uint accountId)
    {
        try
        {
            await _tamerNames.InsertOneAsync(new BsonDocument
            {
                { "_id", NormalizeName(name) },
                { "accountId", (long)accountId }
            });
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task ReleaseTamerNameAsync(string name)
    {
        await _tamerNames.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", NormalizeName(name)));
    }

    /// <summary>
    /// Reconstruye la colección de nombres reservados a partir de los personajes que existen de verdad.
    /// Llamar al arrancar el Character Server: registra los personajes antiguos y libera nombres
    /// de personajes que hayas borrado a mano en Mongo.
    /// </summary>
    public async Task SyncTamerNamesAsync()
    {
        var accounts = await _accounts.Find(FilterDefinition<GameAccount>.Empty).ToListAsync();

        var owners = new Dictionary<string, uint>();
        foreach (var account in accounts)
        {
            foreach (var character in account.Characters ?? new List<CharacterDocument>())
            {
                owners.TryAdd(NormalizeName(character.Name), account.AccountId);
            }
        }

        var filters = Builders<BsonDocument>.Filter;

        // Fuera las reservas huérfanas
        await _tamerNames.DeleteManyAsync(filters.Nin("_id", owners.Keys));

        // Dentro los nombres que existan y no estén reservados
        foreach (var (name, owner) in owners)
        {
            await _tamerNames.UpdateOneAsync(
                filters.Eq("_id", name),
                Builders<BsonDocument>.Update.SetOnInsert("accountId", (long)owner),
                new UpdateOptions { IsUpsert = true });
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[MongoDB] Nombres de tamer sincronizados: {owners.Count}");
        Console.ResetColor();
    }

    // ==========================================
    // COLECCIONES Y MÉTODOS DEL MAPA / MONSTRUOS
    // ==========================================

    // Colección de los spawns
    public IMongoCollection<MobSpawnDocument> MobSpawns => _database.GetCollection<MobSpawnDocument>("mob_spawns");

    // Colección de las plantillas (Stats) de los monstruos
    public IMongoCollection<MonsterTemplateDocument> MonsterTemplates => _database.GetCollection<MonsterTemplateDocument>("monster_templates");

    // Método para obtener todos los monstruos de un mapa específico
    public async Task<List<MobSpawnDocument>> GetSpawnsByMapIdAsync(int mapId)
    {
        var filter = Builders<MobSpawnDocument>.Filter.Eq(x => x.MapId, mapId);
        return await MobSpawns.Find(filter).ToListAsync();
    }

    // Método para buscar los stats base de una especie de monstruo
    public async Task<MonsterTemplateDocument?> GetMonsterTemplateAsync(uint monsterId)
    {
        int idInt = (int)monsterId;
        long idLong = (long)monsterId;
        string idStr = monsterId.ToString();

        var fields = new[] { "MonsterID", "monster_id", "MonsterId", "ModelDigimon", "model_digimon", "s_dwID", "s_dwMonsterID", "dwMonsterID", "id", "Id" };
        var filters = new List<FilterDefinition<MonsterTemplateDocument>>();

        foreach (var f in fields)
        {
            filters.Add(Builders<MonsterTemplateDocument>.Filter.Eq(f, idInt));
            filters.Add(Builders<MonsterTemplateDocument>.Filter.Eq(f, idLong));
            filters.Add(Builders<MonsterTemplateDocument>.Filter.Eq(f, idStr));
        }

        var filter = Builders<MonsterTemplateDocument>.Filter.Or(filters);
        return await MonsterTemplates.Find(filter).FirstOrDefaultAsync();
    }

    // ==========================================
    // COLECCIÓN Y MÉTODOS DE DIGIMON PARTNER
    // ==========================================

    // Colección de plantillas de Digimon Partner (de Digimon_List.xml)
    public IMongoCollection<DigimonTemplateDocument> DigimonTemplates => _database.GetCollection<DigimonTemplateDocument>("digimon_templates");

    // Método para buscar la plantilla de un Digimon Partner por su Model o Species
    public async Task<DigimonTemplateDocument?> GetDigimonTemplateAsync(int modelOrSpecies)
    {
        int valInt = modelOrSpecies;
        long valLong = (long)modelOrSpecies;
        string valStr = modelOrSpecies.ToString();

        var fields = new[] { "Species", "Model", "species", "model", "s_dwSpecies", "s_dwModel", "s_dwID", "dwSpecies", "dwModel" };
        var filters = new List<FilterDefinition<DigimonTemplateDocument>>();

        foreach (var f in fields)
        {
            filters.Add(Builders<DigimonTemplateDocument>.Filter.Eq(f, valInt));
            filters.Add(Builders<DigimonTemplateDocument>.Filter.Eq(f, valLong));
            filters.Add(Builders<DigimonTemplateDocument>.Filter.Eq(f, valStr));
        }

        var filter = Builders<DigimonTemplateDocument>.Filter.Or(filters);
        return await DigimonTemplates.Find(filter).FirstOrDefaultAsync();
    }

    // ==========================================
    // COLECCIÓN Y MÉTODOS DE SKILLS (Skill.xml)
    // ==========================================

    // Colección de plantillas de Skills (de Skill.xml)
    public IMongoCollection<SkillTemplateDocument> SkillTemplates => _database.GetCollection<SkillTemplateDocument>("skill_templates");

    // Método para buscar la información oficial de una habilidad por su s_dwID / SkillId (soporta int o string)
    public async Task<SkillTemplateDocument?> GetSkillTemplateAsync(int skillId)
    {
        string skillIdStr = skillId.ToString();
        var filter = Builders<SkillTemplateDocument>.Filter.Or(
            Builders<SkillTemplateDocument>.Filter.Eq("s_dwID", skillId),
            Builders<SkillTemplateDocument>.Filter.Eq("s_dwID", skillIdStr),
            Builders<SkillTemplateDocument>.Filter.Eq("SkillId", skillId),
            Builders<SkillTemplateDocument>.Filter.Eq("SkillId", skillIdStr),
            Builders<SkillTemplateDocument>.Filter.Eq("skill_id", skillId),
            Builders<SkillTemplateDocument>.Filter.Eq("skill_id", skillIdStr)
        );
        return await SkillTemplates.Find(filter).FirstOrDefaultAsync();
    }

    // ==========================================
    // COLECCIÓN Y MÉTODOS DE PORTALES (MapPortal.xml)
    // ==========================================

    // Colección de portales y puntos de teletransporte
    public IMongoCollection<PortalTemplateDocument> Portals => _database.GetCollection<PortalTemplateDocument>("portals");

    /// <summary>
    /// Busca un portal por su PortalId (soporta int o string, y campos s_dwPortalID / portalId).
    /// Permite a los administradores meter nuevos mapas y portales directamente en la base de datos.
    /// </summary>
    public async Task<PortalTemplateDocument?> GetPortalAsync(int portalId)
    {
        string portalIdStr = portalId.ToString();
        var filter = Builders<PortalTemplateDocument>.Filter.Or(
            Builders<PortalTemplateDocument>.Filter.Eq("portalId", portalId),
            Builders<PortalTemplateDocument>.Filter.Eq("portalId", portalIdStr),
            Builders<PortalTemplateDocument>.Filter.Eq("PortalId", portalId),
            Builders<PortalTemplateDocument>.Filter.Eq("s_dwPortalID", portalId),
            Builders<PortalTemplateDocument>.Filter.Eq("s_dwPortalID", portalIdStr)
        );
        return await Portals.Find(filter).FirstOrDefaultAsync();
    }
}