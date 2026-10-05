namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using dmoserver.database;
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

    // Guardamos la cuenta y personaje activo de la sesión
    private GameAccount? _account;
    private CharacterDocument? _currentCharacter;

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

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[<-] Paquete recibido: Opcode {packet.Type} (Tamaño: {packetLength})");
                    Console.ResetColor();

                    await ProcessPacketAsync(packet, packetData, packetLength);
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

    private async Task ProcessPacketAsync(GamePacketReader packet, byte[] rawBuffer, short packetLength)
    {
        switch (packet.Type)
        {
            case -1: // Handshake response
            {
                var kind = packet.ReadByte();
                var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var handshake = (short)(_clientHandshake ^ HandshakeDegree);

                byte[] connResponse = new ConnectionPacket(handshake, handshakeTimestamp).Serialize();
                await SendAsync(connResponse);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[->] ConnectionPacket (-2) enviado confirmando el Handshake.");
                Console.ResetColor();
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

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[+] Solicitud de entrada al mapa para AccountId: {accountId}");
                Console.ResetColor();

                // 1. Obtener o crear cuenta y personaje directamente en MongoDB
                _account = await Db.GetOrCreateAccountAsync(accountId);

                // Búsqueda segura: slot activo o primer personaje existente
                _currentCharacter = _account.Characters?.FirstOrDefault(c => c.Slot == _account.LastPlayedSlot)
                                 ?? _account.Characters?.FirstOrDefault();

                if (_currentCharacter == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[-] [Game] La cuenta {accountId} no tiene personajes registrados en MongoDB. Abortando entrada.");
                    Console.ResetColor();
                    Socket.Close();
                    return;
                }

                // 2. InitialInfoPacket construido con los datos reales de MongoDB
                byte[] initialInfo = new InitialInfoPacket(
                    _currentCharacter.Name,
                    _currentCharacter.Partner.Name,
                    _currentCharacter.Model,
                    _currentCharacter.Partner.Model
                ).Serialize();

                await SendAsync(initialInfo);

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[✓] InitialInfoPacket (1003) enviado desde MongoDB ({_currentCharacter.Name} / {_currentCharacter.Partner.Name}).");
                Console.WriteLine("[*] Esperando respuesta del cliente (Opcode 1001)...");
                Console.ResetColor();
                break;
            }

            case 1001: // ComplementarInformation: El cliente cargó el mapa
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Cliente confirmó carga del mapa (Opcode 1001 recibido).");
                Console.ResetColor();

                if (_currentCharacter != null)
                {
                    uint tamerHandle = (uint)(100000 + _currentCharacter.Slot);
                    uint partnerHandle = (uint)(200000 + _currentCharacter.Slot);

                    // 1. Spawn del Tamer y Partner usando los datos de MongoDB
                    byte[] loadTamer = new LoadTamerPacket(
                        _currentCharacter.Name,
                        _currentCharacter.Partner.Name,
                        _currentCharacter.Model,
                        _currentCharacter.Partner.Model
                    ).Serialize();

                    await SendAsync(loadTamer);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] LoadTamerPacket (1006) enviado.");
                    Console.ResetColor();

                    // 2. Establecer la velocidad de movimiento base para desbloquear el desplazamiento
                    byte[] speedPacket = new UpdateMovementSpeedPacket(tamerHandle, partnerHandle, 600).Serialize();
                    await SendAsync(speedPacket);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[->] UpdateMovementSpeedPacket (9905) enviado (Velocidad: 600).");
                    Console.ResetColor();
                }
                break;
            }

          case 1004: // SyncMovement: Movimiento calibrado (26 bytes)
            {
                uint sequence = packet.ReadUInt();
                short movementFlag = packet.ReadShort();
                short subType = packet.ReadShort();
                int coordX = packet.ReadInt();
                int coordY = packet.ReadInt();
                float yaw = BitConverter.Int32BitsToSingle(packet.ReadInt());

                uint tamerHandle = _currentCharacter != null ? (uint)(100000 + _currentCharacter.Slot) : 100000;
                uint partnerHandle = _currentCharacter != null ? (uint)(200000 + _currentCharacter.Slot) : 200000;

                // 1. Enviar movimiento del Tamer
                byte[] tamerWalk = new TamerWalkPacket(coordX, coordY, tamerHandle).Serialize();
                await SendAsync(tamerWalk);

                // 2. Calcular posición de seguimiento del Digimon (~140 unidades detrás según Yaw)
                const double followDistance = 140.0;
                int digimonX = coordX - (int)(Math.Cos(yaw) * followDistance);
                int digimonY = coordY - (int)(Math.Sin(yaw) * followDistance);

                // 3. Enviar movimiento del Partner con su propio paquete
                byte[] partnerWalk = new PartnerWalkPacket(digimonX, digimonY, partnerHandle).Serialize();
                await SendAsync(partnerWalk);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[->] Movimiento Tamer: ({coordX}, {coordY}) | Digimon: ({digimonX}, {digimonY})");
                Console.ResetColor();

                if (_account != null && _currentCharacter != null)
                {
                    _ = Db.UpdatePositionAsync(_account.AccountId, _currentCharacter.Slot, coordX, coordY, yaw);
                }
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

    public async Task SendAsync(byte[] data)
    {
        if (Socket.Connected)
        {
            await _stream.WriteAsync(data.AsMemory());
            await _stream.FlushAsync();
        }
    }
}