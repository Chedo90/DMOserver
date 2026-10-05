namespace dmoserver.database;

using MongoDB.Driver;

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
            var nextAccountId = (uint)(await _accounts.CountDocumentsAsync(FilterDefinition<GameAccount>.Empty) + 1);

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

    public async Task<GameAccount?> GetAccountByIdAsync(uint accountId)
    {
        return await _accounts.Find(a => a.AccountId == accountId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Método requerido por GAME SERVER y CHARACTER SERVER
    /// </summary>
    public async Task<GameAccount> GetOrCreateAccountAsync(uint accountId)
    {
        var filter = Builders<GameAccount>.Filter.Eq(a => a.AccountId, accountId);
        var account = await _accounts.Find(filter).FirstOrDefaultAsync();

        if (account == null)
        {
            account = new GameAccount
            {
                AccountId = accountId,
                Username = $"Player_{accountId}",
                PasswordHash = "demo",
                Characters =
                [
                    new CharacterDocument
                    {
                        Slot = 0,
                        Name = "Marcus",
                        Model = 80001,
                        Partner = new PartnerDigimonDocument
                        {
                            Name = "Agumon",
                            Model = 31001
                        }
                    }
                ]
            };

            await _accounts.InsertOneAsync(account);
        }

        return account;
    }

    /// <summary>
    /// Actualiza el estado completo de la cuenta (usado al crear personajes o cambiar de slot)
    /// </summary>
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