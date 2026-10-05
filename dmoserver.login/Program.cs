using System.Net;
using System.Net.Sockets;
using System.Text;
using dmoserver.database;
using dmoserver.login.Network;
using dmoserver.login.Packet;

const int port = 7029;
var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

MongoDbContext db = new();

Console.Title = "DMO Custom Login Server";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"=== Servidor de Login iniciado en el puerto {port} ===");
Console.WriteLine("Esperando conexiones...\n");
Console.ResetColor();

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client, db);
}

static async Task HandleClientAsync(TcpClient client, MongoDbContext db)
{
    client.NoDelay = true;
    var endPoint = client.Client.RemoteEndPoint?.ToString();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[+] Cliente conectado: {endPoint}");
    Console.ResetColor();

    using NetworkStream stream = client.GetStream();
    byte[] buffer = new byte[4096];
    GameAccount? currentAccount = null;

    try
    {
        // -------------------------------------------------------------
        // SALUDO 1
        // -------------------------------------------------------------
        short initialHandshakeKey = 1;
        var handshake1 = new PacketWriter(-1);
        handshake1.WriteShort(initialHandshakeKey);
        await stream.WriteAsync(handshake1.Build());

        // -------------------------------------------------------------
        // SALUDO 2
        // -------------------------------------------------------------
        int bytesRead = await stream.ReadAsync(buffer);
        if (bytesRead == 0) return;

        short handshakeDegree = 32321;
        short secondHandshake = (short)(initialHandshakeKey ^ handshakeDegree);
        uint timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var handshake2 = new PacketWriter(-2);
        handshake2.WriteShort(secondHandshake);
        handshake2.WriteUInt(timestamp);
        await stream.WriteAsync(handshake2.Build());

        // -------------------------------------------------------------
        // BUCLE PRINCIPAL DE MENSAJES (Soporte Multi-Paquete)
        // -------------------------------------------------------------
        while (client.Connected)
        {
            bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;

            int offset = 0;
            
            while (offset < bytesRead)
            {
                if (bytesRead - offset < 4) break;

                short length = BitConverter.ToInt16(buffer, offset);
                if (length < 4 || offset + length > bytesRead) break;

                short opcode = BitConverter.ToInt16(buffer, offset + 2);

                switch (opcode)
                {
                    case 3301: // Login Request
                        currentAccount = await HandleLoginRequest(buffer, offset, length, stream, db);
                        if (currentAccount == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[-] [Login] Intento fallido para {endPoint}. Manteniendo socket para reintento.");
                            Console.ResetColor();
                            // NO cerramos el socket; el paquete de fallo ya fue enviado al cliente
                        }
                        break;

                    case 1701: // Server List Request
                        if (currentAccount == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[-] Intento de solicitar ServerList sin sesión autenticada.");
                            Console.ResetColor();
                            client.Close();
                            return;
                        }

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[+] Enviando ServerList a {currentAccount.Username} (ID: {currentAccount.AccountId})...");
                        Console.ResetColor();

                        byte[] serverListResponse = ServerListPacket.Create();
                        await stream.WriteAsync(serverListResponse);
                        break;

                    case 1702: // Selección de servidor
                        if (currentAccount == null)
                        {
                            client.Close();
                            return;
                        }

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[+] Jugador {currentAccount.Username} seleccionó servidor. Enviando pase al Character Server (Opcode 901)...");
                        Console.ResetColor();

                        // Mandamos al cliente al Character Server (puerto 7030) con su AccountId real de Mongo
                        byte[] characterServerResponse = ConnectCharacterServerPacket.Create((long)currentAccount.AccountId, "127.0.0.1", 7030);
                        await stream.WriteAsync(characterServerResponse);
                        break;

                    case -3: // KeepConnection (Heartbeat)
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[<-] Opcode no implementado: {opcode} ({length} bytes)");
                        Console.ResetColor();
                        break;
                }

                offset += length; 
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[!] Error con {endPoint}: {ex.Message}");
        Console.ResetColor();
    }
    finally
    {
        Console.WriteLine($"[-] Cliente desconectado: {endPoint}");
        client.Close();
    }
}

static async Task<GameAccount?> HandleLoginRequest(byte[] buffer, int offset, int length, NetworkStream stream, MongoDbContext db)
{
    int pos = offset + 9;
    if (pos >= offset + length) return null;

    int userLen = buffer[pos++];
    string user = Encoding.ASCII.GetString(buffer, pos, userLen);
    pos += userLen + 1;

    int passLen = buffer[pos++];
    string pass = Encoding.ASCII.GetString(buffer, pos, passLen);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[+] Intento de Login: {user} / {pass}");
    Console.ResetColor();

    // 1. Consultar / Autoregistrar en MongoDB
    var account = await db.AuthenticateOrRegisterAsync(user, pass);

    if (account == null)
    {
        // 2. Respuesta de LOGIN FALLIDO: Notifica fallo sin romper la conexión TCP
       var failResponse = new PacketWriter(3301);
       // Código 101 o 102 dispara el cuadro de diálogo nativo de credenciales incorrectas en el cliente DMO
        failResponse.WriteInt(102); 
        failResponse.WriteByte(0);
        
        await stream.WriteAsync(failResponse.Build());
        return null;
    }

    // 3. Respuesta de LOGIN EXITOSO
    var successResponse = new PacketWriter(3301);
    successResponse.WriteInt(0);
    successResponse.WriteByte(1); // 1 = Aceptado

    await stream.WriteAsync(successResponse.Build());
    return account;
}