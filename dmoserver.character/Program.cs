using System.Net;
using System.Net.Sockets;
using dmoserver.character.Network;
using dmoserver.database;

const int port = 7030;

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

MongoDbContext db = new();

// Registra los nombres de personajes existentes y libera los de personajes borrados a mano en Mongo
await db.SyncTamerNamesAsync();

Console.Title = "DMO Custom Character Server";
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"=== Character Server activo y escuchando en el puerto {port} ===");
Console.WriteLine("Esperando transferencia desde el Login...\n");
Console.ResetColor();

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = new CharacterClient(client, db).StartAsync();
}