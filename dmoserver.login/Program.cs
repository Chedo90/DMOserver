using System.Net;
using System.Net.Sockets;
using System.Text;
using dmoserver.login.Network;
using dmoserver.login.Packet;


const int port = 7029;
var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

Console.Title = "DMO Custom Login Server";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"=== Servidor de Login iniciado en el puerto {port} ===");
Console.WriteLine("Esperando conexiones...\n");
Console.ResetColor();

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client);
}

static async Task HandleClientAsync(TcpClient client)
{
    client.NoDelay = true;
    var endPoint = client.Client.RemoteEndPoint?.ToString();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[+] Cliente conectado: {endPoint}");
    Console.ResetColor();

    using NetworkStream stream = client.GetStream();
    byte[] buffer = new byte[4096];

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
            
            // Iteramos mientras queden bytes sin leer en el buffer
            while (offset < bytesRead)
            {
                if (bytesRead - offset < 4) break; // Faltan bytes para la cabecera

                short length = BitConverter.ToInt16(buffer, offset);
                if (length < 4 || offset + length > bytesRead) break; // Paquete incompleto

                short opcode = BitConverter.ToInt16(buffer, offset + 2);

                switch (opcode)
                {
                    case 3301: // Login Request
                        await HandleLoginRequest(buffer, offset, length, stream);
                        break;

                    case 1701: // Server List Request
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[+] Cliente solicita lista de mundos. Enviando ServerList...");
                        Console.ResetColor();

                        byte[] serverListResponse = ServerListPacket.Create();
                        await stream.WriteAsync(serverListResponse);
                        break;

                    case 1702: // Selección de servidor
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[+] Jugador seleccionó servidor. Enviando pase al Character Server (Opcode 901)...");
                        Console.ResetColor();

                        // Mandamos al cliente al puerto 7030
                        byte[] characterServerResponse = ConnectCharacterServerPacket.Create(1, "127.0.0.1", 7030);
                        await stream.WriteAsync(characterServerResponse);
                        break;

                    case -3: // KeepConnection (Heartbeat)
                        // Lo ignoramos silenciosamente
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[<-] Opcode no implementado: {opcode} ({length} bytes)");
                        Console.ResetColor();
                        break;
                }

                // Movemos el puntero al inicio del siguiente paquete
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

// Nota: Hemos actualizado los offsets internos para que sumen el offset del buffer
static async Task HandleLoginRequest(byte[] buffer, int offset, int length, NetworkStream stream)
{
    int pos = offset + 9;
    if (pos >= offset + length) return;

    int userLen = buffer[pos++];
    string user = Encoding.ASCII.GetString(buffer, pos, userLen);
    pos += userLen + 1;

    int passLen = buffer[pos++];
    string pass = Encoding.ASCII.GetString(buffer, pos, passLen);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[+] Autenticación: {user} / {pass}");
    Console.ResetColor();

    var response = new PacketWriter(3301);
    response.WriteInt(0);
    response.WriteByte(1);

    await stream.WriteAsync(response.Build());
}