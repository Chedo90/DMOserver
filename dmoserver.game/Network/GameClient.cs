namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dmoserver.database;

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
    private string? _selectedCharacterName;

    // Variables de posición y estado
    private int _currentTamerX;
    private int _currentTamerY;
    private bool _testMobSpawned = false;

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
            _clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler);

            using (var onConnectWriter = new PacketWriter(65535))
            {
                onConnectWriter.WriteShort(_clientHandshake);
                byte[] onConnectPacket = onConnectWriter.Serialize();
                await SendAsync(onConnectPacket);
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[->] OnConnect inicial enviado. Handshake: {_clientHandshake}");
            Console.ResetColor();

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

                    if (payloadLength >= 7)
                    {
                        string headerText = Encoding.ASCII.GetString(packetData, 2, Math.Min(7, payloadLength));
                        if (headerText.Equals("DMIPASS", StringComparison.Ordinal))
                        {
                            string fullPayload = Encoding.ASCII.GetString(packetData, 2, payloadLength);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[GameServer] Paquete DMIPASS interceptado correctamente.");
                            Console.ResetColor();

                            string[] parts = fullPayload.Split(' ');
                            if (parts.Length > 1)
                            {
                                _sessionToken = parts[1];
                                _account = await Db.GetAccountBySessionTokenAsync(_sessionToken);

                                if (_account != null)
                                {
                                    _accountId = _account.AccountId;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Auth] Sesión validada para la cuenta ID {_accountId}");
                                    Console.ResetColor();
                                }
                            }
                            continue;
                        }
                    }

                    var packet = new GamePacketReader(packetData, packetLength);
                    if (packet.Type != 1004 && packet.Type != -3)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[<-] Paquete recibido: Opcode {packet.Type} (Tamaño: {packetLength})");
                        Console.ResetColor();
                    }

                    await ProcessPacketAsync(packet, packetData);
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
            Console.WriteLine($"[-] Error con {_endPoint}: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            if (_account != null && _currentCharacter != null && _currentTamerX > 0 && _currentTamerY > 0)
            {
                _ = Db.UpdatePositionAsync(_account.AccountId, _currentCharacter.Slot, _currentTamerX, _currentTamerY, 0f);
            }
            Socket.Close();
            Console.WriteLine($"[-] Conexión con {_endPoint} cerrada.");
        }
    }

    private async Task ProcessPacketAsync(GamePacketReader packet, byte[] rawData)
    {
        switch (packet.Type)
        {
            case -1:
            {
                packet.ReadByte();
                var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var handshake = (short)(_clientHandshake ^ HandshakeDegree);
                byte[] connResponse = new dmoserver.game.Packets.ConnectionPacket(handshake, handshakeTimestamp).Serialize();
                await SendAsync(connResponse);
                break;
            }

            case -3:
                break;

            case 1302:
            {
                _selectedCharacterName = packet.ReadString();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Selección] El cliente eligió al Tamer: {_selectedCharacterName}");
                Console.ResetColor();
                break;
            }

            case 1706: // InitialInformation
            {
                if (_account == null)
                {
                    uint accountIdToSearch = _accountId > 0 ? _accountId : 1;
                    _account = await Db.GetAccountByIdAsync(accountIdToSearch)
                               ?? await Db.GetOrCreateAccountAsync(accountIdToSearch);
                    if (_account != null) _accountId = _account.AccountId;
                }

                if (_account == null) break;

                var freshAccount = await Db.GetAccountByIdAsync(_account.AccountId);
                if (freshAccount != null) _account = freshAccount;

                _account.Characters ??= [];
                if (_account.Characters.Count == 0) break;

                if (!string.IsNullOrEmpty(_selectedCharacterName))
                    _currentCharacter = _account.Characters.FirstOrDefault(c => c.Name.Equals(_selectedCharacterName, StringComparison.OrdinalIgnoreCase));

                _currentCharacter ??= _account.Characters.FirstOrDefault(c => c.Slot == _account.LastPlayedSlot);
                _currentCharacter ??= _account.Characters.OrderByDescending(c => c.Slot).FirstOrDefault() ?? _account.Characters[0];

                if (_currentCharacter.Location == null || (_currentCharacter.Location.X == 0 && _currentCharacter.Location.Y == 0))
                {
                    _currentCharacter.Location = new CharacterLocation { MapId = _currentCharacter.Location?.MapId ?? 1, X = 27993, Y = 29095, Z = 0f };
                }

                _currentTamerX = _currentCharacter.Location.X;
                _currentTamerY = _currentCharacter.Location.Y;

                if (_currentCharacter.Partner == null) break;

                int mapId = _currentCharacter.Location.MapId;

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[Login] Cargando Tamer: {_currentCharacter.Name} en Mapa {mapId}");
                Console.ResetColor();

                byte[] initialInfo = new dmoserver.game.Packets.InitialInfoPacket(
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

                // Sincronización de vida y maná completos
                var tamerStatus = new dmoserver.game.Packets.UpdateStatusPacket(TamerHandle, 1000, 1000, 500, 500);
                await SendAsync(tamerStatus.Serialize());

                var partnerStatus = new dmoserver.game.Packets.UpdateStatusPacket(PartnerHandle, 1000, 1000, 500, 500);
                await SendAsync(partnerStatus.Serialize());

                break;
            }

case 1001:
{
    if (!_testMobSpawned && _currentCharacter != null)
    {
        int mapId = _currentCharacter.Location.MapId;
        var mapSpawns = await Db.GetSpawnsByMapIdAsync(mapId);

        uint idx = 0;
        foreach (var spawn in mapSpawns)
        {
            if (idx > 0x3FFF) break; // límite del rango de mobs

            uint mobHandle = 0x10000u | idx++;

            await SendAsync(new dmoserver.game.Packets.MobSpawnPacket(
                mobHandle, spawn.MonsterId, spawn.X, spawn.Y, 1).Serialize());
            await SendAsync(new dmoserver.game.Packets.MobStatePacket(mobHandle).Serialize());

            Console.WriteLine($"[Mongo] Type {spawn.MonsterId} X:{spawn.X} Y:{spawn.Y} handle=0x{mobHandle:X}");
        }

        _testMobSpawned = true;
    }
    break;
}

            case 1713: // Solo log, no hace nada por ahora
            {
                Console.WriteLine("[1713] recibido (ignorado en esta prueba)");
                break;
            }

            case 1004: // SyncMovement
            {
                packet.ReadUInt();
                packet.ReadShort();
                packet.ReadShort();
                int destX = packet.ReadInt();
                int destY = packet.ReadInt();
                float yaw = BitConverter.Int32BitsToSingle(packet.ReadInt());

                _currentTamerX = destX;
                _currentTamerY = destY;

                if (_account != null && _currentCharacter != null)
                {
                    _ = Db.UpdatePositionAsync(_account.AccountId, _currentCharacter.Slot, destX, destY, yaw);
                }
                break;
            }

            case 1013: // Ataque / Combate
            {
                await dmoserver.game.Handlers.CombatHandler.HandleAttackRequestAsync(this, packet);
                break;
            }

            case 1008: // Chat
            {
                await dmoserver.game.Handlers.ChatHandler.HandleAsync(this, packet);
                break;
            }

            case 1709: // Portales
            {
                int portalId = packet.ReadInt();
                short channel = packet.ReadShort();
                await SendAsync(new dmoserver.game.Packets.MapSwapPacket("127.0.0.1", 7035, 89, 25000, 25000).Serialize());
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
        byte[] msg = new dmoserver.game.Packets.ChatMessagePacket(TamerHandle, text, 0).Serialize();
        await SendAsync(msg);
    }

    public async Task TeleportAsync(int x, int y)
    {
        _currentTamerX = x;
        _currentTamerY = y;
        await SendAsync(new dmoserver.game.Packets.TamerTeleportPacket(x, y, TamerHandle).Serialize());
        await SendAsync(new dmoserver.game.Packets.PartnerTeleportPacket(x, y, PartnerHandle).Serialize());
    }

    public async Task SetSpeedAsync(short speed)
    {
        await SendAsync(new dmoserver.game.Packets.UpdateMovementSpeedPacket(TamerHandle, PartnerHandle, speed).Serialize());
    }

    public async Task SendAsync(byte[] data)
    {
        if (Socket.Connected)
        {
            await _stream.WriteAsync(data.AsMemory());
            await _stream.FlushAsync();
        }
    }

    private void LogPacketHex(string title, byte[] packet)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n--- [HEX DUMP: {title}] ---");
        Console.WriteLine($"Longitud total: {packet.Length} bytes");
        Console.WriteLine(BitConverter.ToString(packet).Replace("-", " "));
        Console.WriteLine("------------------------------------------");
        Console.ResetColor();
    }
}