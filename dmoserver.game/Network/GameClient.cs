namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dmoserver.database;
using dmoserver.game.Handlers;
using dmoserver.game.Packets;

public sealed class GameClient(TcpClient socket)
{
    private const short HandshakeDegree = 32321;
    private const int OnConnectEventHandshakeHandler = 65535;

    private static readonly MongoDbContext Db = new();

    public TcpClient Socket { get; } = socket;

    private readonly NetworkStream _stream = socket.GetStream();
    private readonly string _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    private short _clientHandshake;

    private GameAccount? _account;
    private CharacterDocument? _currentCharacter;
    private string? _selectedCharacterName; // Rastrea el nombre del Tamer elegido

    // Tracking de posición para comandos y handlers
    private int _currentTamerX;
    private int _currentTamerY;

    public CharacterDocument? CurrentCharacter => _currentCharacter;
    public int CurrentTamerX => _currentTamerX;
    public int CurrentTamerY => _currentTamerY;
    private uint _accountId;
    private string? _sessionToken;

    public uint TamerHandle => _currentCharacter != null ? (uint)(32768 + _currentCharacter.Slot) : 32768;
    public uint PartnerHandle => _currentCharacter != null ? (uint)(2000 + _currentCharacter.Slot) : 2000;

    public async Task StartAsync()
    {
        Socket.NoDelay = true;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[+] Cliente conectado desde: {_endPoint}");
        Console.ResetColor();

        try
        {
            // 1. Handshake inicial OnConnect (Type = 65535 / -1)
            _clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler);

            using (var onConnectWriter = new PacketWriter(65535))
            {
                onConnectWriter.WriteShort(_clientHandshake);
                byte[] onConnectPacket = onConnectWriter.Serialize();
                await SendAsync(onConnectPacket);
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[->] OnConnect inicial enviado (8 bytes). Handshake: {_clientHandshake}");
            Console.ResetColor();

            // 2. Bucle de recepción de paquetes TCP
            byte[] lengthBuffer = new byte[2];

            while (Socket.Connected)
            {
                try
                {
                    await _stream.ReadExactlyAsync(lengthBuffer.AsMemory(0, 2));
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                short packetLength = BitConverter.ToInt16(lengthBuffer, 0);
                if (packetLength < 4) continue;

                byte[] packetData = ArrayPool<byte>.Shared.Rent(packetLength);
                try
                {
                    Array.Copy(lengthBuffer, 0, packetData, 0, 2);

                    int payloadLength = packetLength - 2;
                    if (payloadLength > 0)
                    {
                        await _stream.ReadExactlyAsync(packetData.AsMemory(2, payloadLength));
                    }

                    // --- INTERCEPCIÓN LIMPIA DE DMIPASS ---
                    if (payloadLength >= 7)
                    {
                        string headerText = Encoding.ASCII.GetString(packetData, 2, Math.Min(7, payloadLength));
                        if (headerText.Equals("DMIPASS", StringComparison.Ordinal))
                        {
                            string fullPayload = Encoding.ASCII.GetString(packetData, 2, payloadLength);
                            
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[GameServer] Paquete DMIPASS recibido e interceptado correctamente.");
                            Console.ResetColor();

                            string[] parts = fullPayload.Split(' ');
                            if (parts.Length > 1)
                            {
                                _sessionToken = parts[1];

                                // Carga dinámica de la cuenta usando el token de sesión
                                _account = await Db.GetAccountBySessionTokenAsync(_sessionToken);

                                if (_account != null)
                                {
                                    _accountId = _account.AccountId;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Auth] Sesión validada para la cuenta ID {_accountId}");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[Auth] No se encontró cuenta para el token de sesión: {_sessionToken}");
                                    Console.ResetColor();
                                }
                            }

                            continue;
                        }
                    }
                    // ----------------------------------------

                    var packet = new GamePacketReader(packetData, packetLength);
                    if (packet.Type != 1004 && packet.Type != -3)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[<-] Paquete recibido: Opcode {packet.Type} (Tamaño: {packetLength})");
                        Console.ResetColor();
                    }

                    await ProcessPacketAsync(packet);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(packetData);
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[-] Error con {_endPoint}: {ex}");
            Console.ResetColor();
        }
        finally
        {
            if (_account != null && _currentCharacter != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Logout] Guardando posición final antes de cerrar -> X: {_currentTamerX}, Y: {_currentTamerY}");
                Console.ResetColor();

                _ = Db.UpdatePositionAsync(_account.AccountId, _currentCharacter.Slot, _currentTamerX, _currentTamerY, 0f);
            }

            Socket.Close();
            Console.WriteLine($"[-] Conexión con {_endPoint} cerrada.");
        }
    } // Fin de StartAsync()

    private async Task ProcessPacketAsync(GamePacketReader packet)
    {
        switch (packet.Type)
        {
            case -1: // Handshake / KeepAlive inicial
            {
                packet.ReadByte();

                var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var handshake = (short)(_clientHandshake ^ HandshakeDegree);

                byte[] connResponse = new ConnectionPacket(handshake, handshakeTimestamp).Serialize();
                await SendAsync(connResponse);
                break;
            }

            case -3: // KeepConnection
            {
                break;
            }

            case 1302: // Opcode 16 05 (0x0516) - Selección de personaje
            {
                _selectedCharacterName = packet.ReadString();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Selección] El cliente eligió al Tamer: {_selectedCharacterName}");
                Console.ResetColor();
                break;
            }

           case 1706: // InitialInformation: El cliente solicita entrar al mapa
{
    if (_account == null)
    {
        uint accountIdToSearch = _accountId > 0 ? _accountId : 1;
        _account = await Db.GetAccountByIdAsync(accountIdToSearch) 
                   ?? await Db.GetOrCreateAccountAsync(accountIdToSearch);

        if (_account != null)
        {
            _accountId = _account.AccountId;
        }
    }

    if (_account == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[-] Error crítico: No se encontró la cuenta en MongoDB.");
        Console.ResetColor();
        break;
    }

    // Recargar los datos frescos de la cuenta por si se acaba de crear un personaje
    var freshAccount = await Db.GetAccountByIdAsync(_account.AccountId);
    if (freshAccount != null)
    {
        _account = freshAccount;
    }

    _account.Characters ??= [];

    if (_account.Characters.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[-] Error: La cuenta {_account.AccountId} no tiene personajes.");
        Console.ResetColor();
        break;
    }

    // 1. Buscar por nombre si vino en el paquete 1302
    if (!string.IsNullOrEmpty(_selectedCharacterName))
    {
        _currentCharacter = _account.Characters.FirstOrDefault(c => 
            c.Name.Equals(_selectedCharacterName, StringComparison.OrdinalIgnoreCase));
    }

    // 2. Si no se encontró por nombre, buscar por LastPlayedSlot
    if (_currentCharacter == null)
    {
        _currentCharacter = _account.Characters.FirstOrDefault(c => c.Slot == _account.LastPlayedSlot);
    }

    // 3. Si aún es null, tomar el de mayor slot (el recién creado suele ser el último)
    if (_currentCharacter == null)
    {
        _currentCharacter = _account.Characters.OrderByDescending(c => c.Slot).FirstOrDefault();
    }

    // Fallback final
    _currentCharacter ??= _account.Characters[0];

    // Asegurar coordenadas de spawn válidas (evitar 30000, 30000 si está bugeado)
    if (_currentCharacter.Location == null || (_currentCharacter.Location.X == 0 && _currentCharacter.Location.Y == 0))
    {
        // Coordenadas típicas de inicio seguro (ajusta según el mapa de inicio de tu servidor):
        _currentCharacter.Location = new CharacterLocation 
        { 
            MapId = _currentCharacter.Location?.MapId ?? 1, 
            X = 15000, 
            Y = 15000, 
            Z = 0f 
        };
    }

    if (_currentCharacter.Partner == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[-] Error crítico: Partner es null.");
        Console.ResetColor();
        break;
    }

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"[Login] Cargando Tamer con éxito: {_currentCharacter.Name} (Slot: {_currentCharacter.Slot}, Pos: {_currentCharacter.Location.X}, {_currentCharacter.Location.Y})");
    Console.ResetColor();

    byte[] initialInfo = new InitialInfoPacket(
        _currentCharacter.Name,
        _currentCharacter.Partner.Name,
        _currentCharacter.Model,
        _currentCharacter.Partner.Model,
        TamerHandle,
        PartnerHandle,
        _currentCharacter.Location.X,
        _currentCharacter.Location.Y
    ).Serialize();

    await SendAsync(initialInfo);
    break;
}

            case 1004: // SyncMovement: Movimiento sincronizado
            {
                packet.ReadUInt();  // sequence
                packet.ReadShort(); // movementFlag
                packet.ReadShort(); // subType
                int destX = packet.ReadInt();
                int destY = packet.ReadInt();
                float yaw = BitConverter.Int32BitsToSingle(packet.ReadInt());

                const double followOffset = 140.0;
                int targetDigimonX = destX - (int)(Math.Cos(yaw) * followOffset);
                int targetDigimonY = destY - (int)(Math.Sin(yaw) * followOffset);

                _currentTamerX = destX;
                _currentTamerY = destY;

                if (_account != null && _currentCharacter != null)
                {
                    _ = Db.UpdatePositionAsync(_account.AccountId, _currentCharacter.Slot, destX, destY, yaw);
                }
                break;
            }

            case 1008: // Chat entrante
            {
                await ChatHandler.HandleAsync(this, packet);
                break;
            }

            case 1709: // Petición de portal / teleport
            {
                int portalId = packet.ReadInt();
                short channel = packet.ReadShort();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[Portal 1709] PortalId: {portalId}, Canal: {channel}. Redirigiendo a MapServer en puerto 7035...");
                Console.ResetColor();

                string mapServerIp = "127.0.0.1";
                int mapServerPort = 7035; 
                int targetMapId = 89;     
                int targetX = 25000;
                int targetY = 25000;

                await SendAsync(new MapSwapPacket(mapServerIp, mapServerPort, targetMapId, targetX, targetY).Serialize());
                break;
            }

            default:
            {
                if (packet.Type != 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"[!] Opcode no manejado: {packet.Type} (Tamaño: {packet.Length})");
                    Console.ResetColor();
                }
                break;
            }
        }
    }

    public async Task SendChatMessageAsync(string text)
    {
        byte[] msg = new ChatMessagePacket(TamerHandle, text, ChatType.Normal).Serialize();
        await SendAsync(msg);
    }

    public async Task TeleportAsync(int x, int y)
    {
        _currentTamerX = x;
        _currentTamerY = y;

        await SendAsync(new TamerTeleportPacket(x, y, TamerHandle).Serialize());
        await SendAsync(new PartnerTeleportPacket(x, y, PartnerHandle).Serialize());
    }

    public async Task SetSpeedAsync(short speed)
    {
        await SendAsync(new UpdateMovementSpeedPacket(TamerHandle, PartnerHandle, speed).Serialize());
    }

    public async Task SendAsync(byte[] data)
    {
        if (Socket.Connected)
        {
            await _stream.WriteAsync(data.AsMemory());
            await _stream.FlushAsync();
        }
    }
}