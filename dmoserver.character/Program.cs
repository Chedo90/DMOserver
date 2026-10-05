using System.Net;
using System.Net.Sockets;
using dmoserver.character.Enums;
using dmoserver.character.Network;
using dmoserver.character.Packets;
using dmoserver.database;

const int port = 7030;
const int HandshakeDegree = 32321;
const int OnConnectEventHandshakeHandler = 65535;

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

MongoDbContext db = new();

Console.Title = "DMO Custom Character Server";
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"=== Character Server activo y escuchando en el puerto {port} ===");
Console.WriteLine("Esperando transferencia desde el Login...\n");
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
    Console.WriteLine($"\n[+] Cliente conectado al Character Server desde: {endPoint}");
    Console.ResetColor();

    using NetworkStream stream = client.GetStream();

    // 1. EVENTO ONCONNECT (Opcode 65535)
    short clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler);
    byte[] onConnectPacket = new OnConnectEventConnectionPacket(clientHandshake).Serialize();
    await stream.WriteAsync(onConnectPacket, 0, onConnectPacket.Length);
    await stream.FlushAsync();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[->] OnConnectEventConnectionPacket (65535) enviado. Handshake: {clientHandshake}");
    Console.ResetColor();

    byte[] lengthBuffer = new byte[2];
    uint currentAccountId = 0;

    try
    {
        while (client.Connected)
        {
            // Leemos la longitud del paquete entrante (primeros 2 bytes)
            int read = await stream.ReadAsync(lengthBuffer, 0, 2);
            if (read < 2) break;

            short packetLength = BitConverter.ToInt16(lengthBuffer, 0);
            byte[] packetData = new byte[packetLength];
            Array.Copy(lengthBuffer, 0, packetData, 0, 2);

            int totalRead = 2;
            while (totalRead < packetLength)
            {
                int r = await stream.ReadAsync(packetData, totalRead, packetLength - totalRead);
                if (r == 0) break;
                totalRead += r;
            }

            var packet = new CharacterPacketReader(packetData);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[<-] Paquete recibido: {packet.Enum} (Opcode: {packet.Type}, Tamaño: {packet.Length})");
            Console.ResetColor();

            switch (packet.Enum)
            {
                case CharacterServerPacketEnum.Connection: // Opcode -1
                {
                    var kind = packet.ReadByte();
                    var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var handshake = (short)(clientHandshake ^ HandshakeDegree);

                    byte[] connResponse = new ConnectionPacket(handshake, handshakeTimestamp).Serialize();
                    await stream.WriteAsync(connResponse, 0, connResponse.Length);
                    await stream.FlushAsync();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] ConnectionPacket (-2) enviado confirmando el Handshake.");
                    Console.ResetColor();
                    break;
                }

                case CharacterServerPacketEnum.KeepConnection: // Opcode -3
                    break;

                case CharacterServerPacketEnum.RequestCharacters: // Opcode 1706
                {
                    packet.Seek(8);
                    currentAccountId = packet.ReadUInt();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] El cliente solicita la lista de personajes para el AccountId: {currentAccountId}");
                    Console.ResetColor();

                    // 1. Obtener la cuenta desde MongoDB
                    var account = await db.GetAccountByIdAsync(currentAccountId);

                    // 2. Serializar personajes reales o lista vacía (byte 99) si es cuenta nueva
                    byte[] characterListResponse = new CharacterListPacket(account?.Characters).Serialize();
                    await stream.WriteAsync(characterListResponse, 0, characterListResponse.Length);
                    await stream.FlushAsync();

                    int totalChars = account?.Characters?.Count ?? 0;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[->] CharacterListPacket (1301) enviado con {totalChars} personaje(s) persistido(s).");
                    Console.ResetColor();
                    break;
                }

                case CharacterServerPacketEnum.CheckNameDuplicity: // Opcode 1302
                {
                    string tamerName = packet.ReadString();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[->] Verificando disponibilidad para el nombre de Tamer: '{tamerName}'");
                    Console.ResetColor();

                    bool isAvailable = true;

                    byte[] response = new AvailableNamePacket(isAvailable).Serialize();
                    await stream.WriteAsync(response, 0, response.Length);
                    await stream.FlushAsync();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[->] AvailableNamePacket (1302) enviado: {(isAvailable ? "Disponible" : "Ocupado")}");
                    Console.ResetColor();
                    break;
                }

                case CharacterServerPacketEnum.CreateCharacter: // Opcode 1303
                {
                    // 1. Tamer
                    byte slotPosition = packet.ReadByte();
                    int tamerModel = packet.ReadInt();
                    string tamerName = packet.ReadZString();

                    // 2. Digimon (salto al offset 42 en el paquete entrante)
                    packet.Seek(42);
                    int digimonModel = packet.ReadInt();
                    string digimonName = packet.ReadZString();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] Creando personaje para AccountId {currentAccountId}:");
                    Console.WriteLine($"    Tamer: '{tamerName}' (Modelo: {tamerModel}) en slot {slotPosition}");
                    Console.WriteLine($"    Digimon: '{digimonName}' (Modelo: {digimonModel})");
                    Console.ResetColor();

                    // 3. Persistir en MongoDB
                    if (currentAccountId > 0)
                    {
                        var account = await db.GetOrCreateAccountAsync(currentAccountId);
                        account.Characters ??= new List<CharacterDocument>();

                        var existingChar = account.Characters.FirstOrDefault(c => c.Slot == slotPosition);
                        if (existingChar != null)
                        {
                            account.Characters.Remove(existingChar);
                        }

                        account.Characters.Add(new CharacterDocument
                        {
                            Slot = slotPosition,
                            Name = tamerName,
                            Model = tamerModel,
                            Partner = new PartnerDigimonDocument
                            {
                                Name = digimonName,
                                Model = digimonModel
                            }
                        });

                        account.LastPlayedSlot = slotPosition;
                        await db.UpdateAccountAsync(account);

                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[✓] Personaje '{tamerName}' guardado en MongoDB para la cuenta {currentAccountId}.");
                        Console.ResetColor();
                    }

                    // 4. Handshake del paquete 1306
                    var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var handshake = (short)(handshakeTimestamp & OnConnectEventHandshakeHandler);

                    // 5. Paquete de respuesta 1306
                    byte[] createdPacket = new CharacterCreatedPacket(
                        slotPosition,
                        tamerModel,
                        tamerName,
                        digimonModel,
                        digimonName,
                        handshake,
                        mapId: 105
                    ).Serialize();

                    await stream.WriteAsync(createdPacket, 0, createdPacket.Length);
                    await stream.FlushAsync();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] CharacterCreatedPacket (1306) enviado. Creación confirmada.");
                    Console.ResetColor();
                    break;
                }

                case CharacterServerPacketEnum.GetCharacterPosition: // Opcode 1305
                {
                    byte position = packet.ReadByte();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] El jugador entra al juego con el personaje del slot: {position}");
                    Console.ResetColor();

                    // Marcamos el slot seleccionado en Mongo
                    if (currentAccountId > 0)
                    {
                        var account = await db.GetOrCreateAccountAsync(currentAccountId);
                        account.LastPlayedSlot = position;
                        await db.UpdateAccountAsync(account);
                    }

                    // Enviamos IP, puerto del Game Server (7031) y MapId (105)
                    byte[] infoResponse = new ConnectGameServerInfoPacket("127.0.0.1", "7031", 105).Serialize();
                    await stream.WriteAsync(infoResponse, 0, infoResponse.Length);
                    await stream.FlushAsync();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] ConnectGameServerInfoPacket (1308) enviado con destino a 127.0.0.1:7031 (Mapa 105).");
                    Console.ResetColor();
                    break;
                }

                case CharacterServerPacketEnum.ConnectGameServer: // Opcode 1703
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[+] Cliente solicita autorización para saltar al Game Server.");
                    Console.ResetColor();

                    byte[] connectResponse = new ConnectGameServerPacket().Serialize();
                    await stream.WriteAsync(connectResponse, 0, connectResponse.Length);
                    await stream.FlushAsync();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] ConnectGameServerPacket (1703) enviado. ¡Transferencia al Game Server autorizada!");
                    Console.ResetColor();
                    break;
                }

                default:
                    Console.WriteLine($"[!] Paquete no mapeado: {packet.Enum} ({packet.Type})");
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[-] Sesión finalizada con {endPoint}: {ex.Message}");
        Console.ResetColor();
    }
    finally
    {
        client.Close();
    }
}