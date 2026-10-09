namespace dmoserver.database;

using MongoDB.Driver;
using System;
using System.Threading.Tasks;

public sealed class MongoDbContext
{
    private readonly IMongoCollection<GameAccount> _accounts;

    public MongoDbContext(string connectionString = "mongodb://localhost:27017", string databaseName = "DmoServer")
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _accounts = database.GetCollection<GameAccount>("Accounts");

        var accountIdIndex = new CreateIndexModel<GameAccount>(
            Builders<GameAccount>.IndexKeys.Ascending(a => a.AccountId),
            new CreateIndexOptions { Unique = true }
        );

        var usernameIndex = new CreateIndexModel<GameAccount>(
            Builders<GameAccount>.IndexKeys.Ascending(a => a.Username),
            new CreateIndexOptions { Unique = true }
        );

        _accounts.Indexes.CreateMany([accountIdIndex, usernameIndex]);
    }

    /// <summary>
    /// Métodos para LOGIN SERVER: Autenticar o autoregistrar
    /// </summary>
    public async Task<GameAccount?> AuthenticateOrRegisterAsync(string username, string password)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.Username, username);
        var account = await _accounts.Find(filter).FirstOrDefaultAsync();

        if (account == null)
        {
            // Obtener el AccountId más alto para evitar colisiones de IDs únicos
            var highestAccount = await _accounts
                .Find(FilterDefinition<GameAccount>.Empty)
                .SortByDescending(a => a.AccountId)
                .FirstOrDefaultAsync();

            uint nextAccountId = (highestAccount?.AccountId ?? 0) + 1;

            account = new GameAccount
            {
                AccountId = nextAccountId,
                Username = username,
                PasswordHash = password,
                Characters = [],
                LastPlayedSlot = 0
            };

            await _accounts.InsertOneAsync(account);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[MongoDB] Cuenta registrada con éxito: {username} (ID: {account.AccountId})");
            Console.ResetColor();

            return account;
        }

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

    public async Task<GameAccount?> GetAccountByIdAsync(uint accountId)
    {
        return await _accounts.Find(a => a.AccountId == accountId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Método requerido por GAME SERVER para obtener la cuenta o crearla si la BD está vacía
    /// </summary>
    public async Task<GameAccount> GetOrCreateAccountAsync(uint accountId)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId);
        var account = await _accounts.Find(filter).FirstOrDefaultAsync();

        if (account != null) return account;

        // Si la base de datos se borró o la cuenta no existe, la inicializamos con datos por defecto
        account = new GameAccount
        {
            AccountId = accountId,
            Username = $"User_{accountId}",
            PasswordHash = "admin",
            LastPlayedSlot = 0,
            Characters =
            [
                new CharacterDocument
                {
                    Slot = 0,
                    Name = "takatt",
                    Model = 80001,
                    Level = 1,
                    Location = new CharacterLocation { MapId = 1, X = 30000, Y = 30000, Z = 0f },
                    Partner = new PartnerDigimonDocument
                    {
                        Name = "Agumon",
                        Model = 31001,
                        Level = 1,
                        Size = 10000,
                        HatchGrade = 3
                    }
                }
            ]
        };

        await _accounts.InsertOneAsync(account);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MongoDB] Cuenta autogenerada: ID {accountId} con Tamer 'takatt' y Partner 'Agumon'");
        Console.ResetColor();

        return account;
    }

    public async Task UpdateAccountAsync(GameAccount account)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, account.AccountId);
        await _accounts.ReplaceOneAsync(filter, account, new ReplaceOptions { IsUpsert = true });
    }

    public async Task UpdatePositionAsync(uint accountId, byte slot, int x, int y, float z)
    {
        var filter = Builders<GameAccount>.Filter.And(
            Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId),
            Builders<GameAccount>.Filter.Eq("Characters.Slot", slot)
        );

        var update = Builders<GameAccount>.Update
            .Set("Characters.$.Location.X", x)
            .Set("Characters.$.Location.Y", y)
            .Set("Characters.$.Location.Z", z);

        await _accounts.UpdateOneAsync(filter, update);
    }
}