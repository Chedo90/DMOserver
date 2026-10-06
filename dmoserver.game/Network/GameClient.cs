namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System;
using System.Linq;
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

    // Tracking de posición para comandos y handlers
    private int _currentTamerX;
    private int _currentTamerY;

    public CharacterDocument? CurrentCharacter => _currentCharacter;
    public int CurrentTamerX => _currentTamerX;
    public int CurrentTamerY => _currentTamerY;

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
            Socket.Close();
            Console.WriteLine($"[-] Conexión con {_endPoint} cerrada.");
        }
    }

    private async Task ProcessPacketAsync(GamePacketReader packet)
    {
        switch (packet.Type)
        {
            case -1: // Handshake response
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

            case 1706: // InitialInformation: El cliente solicita entrar al mapa
            {
                packet.Skip(4);
                uint accountId = packet.ReadUInt();

                _account = await Db.GetOrCreateAccountAsync(accountId);
                _currentCharacter = _account.Characters?.FirstOrDefault(c => c.Slot == _account.LastPlayedSlot)
                                 ?? _account.Characters?.FirstOrDefault();

                if (_currentCharacter == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[-] La cuenta {accountId} no tiene personajes. Abortando.");
                    Console.ResetColor();
                    Socket.Close();
                    return;
                }

                byte[] initialInfo = new InitialInfoPacket(
                    _currentCharacter.Name,
                    _currentCharacter.Partner.Name,
                    _currentCharacter.Model,
                    _currentCharacter.Partner.Model
                ).Serialize();

                await SendAsync(initialInfo);
                break;
            }

            case 1001: // ComplementarInformation: Mapa cargado
            {
                if (_currentCharacter != null)
                {
                    uint tamerHandle = (uint)(100000 + _currentCharacter.Slot);
                    uint partnerHandle = (uint)(200000 + _currentCharacter.Slot);

                    _currentTamerX = _currentCharacter.Location.X;
                    _currentTamerY = _currentCharacter.Location.Y;

                    // Spawn inicial de entidades
                    byte[] loadTamer = new LoadTamerPacket(
                        _currentCharacter.Name,
                        _currentCharacter.Partner.Name,
                        _currentCharacter.Model,
                        _currentCharacter.Partner.Model
                    ).Serialize();
                    await SendAsync(loadTamer);

                    // Velocidad base
                    byte[] speedPacket = new UpdateMovementSpeedPacket(tamerHandle, partnerHandle, 600).Serialize();
                    await SendAsync(speedPacket);
                }
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

                uint tamerHandle = _currentCharacter != null ? (uint)(100000 + _currentCharacter.Slot) : 100000;
                uint partnerHandle = _currentCharacter != null ? (uint)(200000 + _currentCharacter.Slot) : 200000;

                // Destino del Digimon a la espalda del Tamer (140 unidades)
                const double followOffset = 140.0;
                int targetDigimonX = destX - (int)(Math.Cos(yaw) * followOffset);
                int targetDigimonY = destY - (int)(Math.Sin(yaw) * followOffset);

                // Enviar desplazamiento de ambas entidades
                await SendAsync(new TamerWalkPacket(destX, destY, tamerHandle).Serialize());
                await SendAsync(new PartnerWalkPacket(targetDigimonX, targetDigimonY, partnerHandle).Serialize());

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
        // Notice (9) o Shout (11) usan Nombre (String), no Handle numérico
        string senderName = "[Servidor]";
        
        byte[] msg = new ChatMessagePacket(text, senderName, ChatType.Notice).Serialize();
        await SendAsync(msg);
    }

    public async Task TeleportAsync(int x, int y)
    {
        uint tamerHandle = _currentCharacter != null ? (uint)(100000 + _currentCharacter.Slot) : 100000;
        uint partnerHandle = _currentCharacter != null ? (uint)(200000 + _currentCharacter.Slot) : 200000;

        _currentTamerX = x;
        _currentTamerY = y;

        await SendAsync(new TamerWalkPacket(x, y, tamerHandle).Serialize());
        await SendAsync(new PartnerWalkPacket(x, y, partnerHandle).Serialize());
    }

    public async Task SetSpeedAsync(short speed)
    {
        uint tamerHandle = _currentCharacter != null ? (uint)(100000 + _currentCharacter.Slot) : 100000;
        uint partnerHandle = _currentCharacter != null ? (uint)(200000 + _currentCharacter.Slot) : 200000;

        await SendAsync(new UpdateMovementSpeedPacket(tamerHandle, partnerHandle, speed).Serialize());
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