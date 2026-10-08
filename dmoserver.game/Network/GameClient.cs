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

    // -----------------------------------------------------------------------
    // HANDLES DINÁMICOS: Centralizamos el DNI del personaje y del Digimon.
    // 32768 (0x8000) es la base obligatoria para que el cliente reconozca Tamers.
    // Sumamos el Slot para que cada personaje de la cuenta tenga un ID único.
    // -----------------------------------------------------------------------
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
                // 1. Cargar cuenta desde la BD si aún no se ha hecho
                if (_account == null)
                {
                    // Usamos ID=1 para las pruebas por ahora
                    _account = await Db.GetOrCreateAccountAsync(1);
                    _currentCharacter = _account.Characters[0];
                }

                // 2. Comprobaciones de seguridad (Evitan que el servidor crashee)
                if (_currentCharacter == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[-] Error crítico: _currentCharacter es null al intentar entrar al mapa.");
                    Console.ResetColor();
                    break;
                }

                if (_currentCharacter.Location == null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[!] Location era null en Mongo. Asignando coordenadas por defecto.");
                    Console.ResetColor();
                    _currentCharacter.Location = new CharacterLocation { X = 30000, Y = 30000, Z = 0 };
                }

                if (_currentCharacter.Partner == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[-] Error crítico: Partner es null en la base de datos.");
                    Console.ResetColor();
                    break;
                }

                // 3. Continuamos con el flujo normal
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[Login] Coordenadas leídas de Mongo -> X: {_currentCharacter.Location.X}, Y: {_currentCharacter.Location.Y}");
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

                // Calculamos el destino del Digimon a la espalda del Tamer (140 unidades)
                // (Es útil dejar este cálculo hecho aquí para cuando hagamos el broadcast a otros jugadores)
                const double followOffset = 140.0;
                int targetDigimonX = destX - (int)(Math.Cos(yaw) * followOffset);
                int targetDigimonY = destY - (int)(Math.Sin(yaw) * followOffset);

                // Actualizamos la memoria del servidor con tu nueva posición
                _currentTamerX = destX;
                _currentTamerY = destY;

                // --- CHIVATO 1: Ver si el juego nos manda movimiento en tiempo real ---
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"[Move] Movimiento recibido -> X: {destX}, Y: {destY}");
                Console.ResetColor();

                // --- NOTA FUTURA: SISTEMA MULTIJUGADOR (BROADCASTING) ---
                // Estos paquetes NO se reenvían al propio jugador ('this.SendAsync') porque su cliente 
                // ya procesa el movimiento de forma local. Si se los devolvemos, causará tirones (rubber-banding).
                // Cuando el servidor soporte más jugadores, aquí deberás iterar sobre los clientes cercanos
                // y enviarles a ELLOS estos paquetes de TamerWalk y PartnerWalk:
                /*
                await BroadcastToNearbyPlayersAsync(new TamerWalkPacket(destX, destY, TamerHandle).Serialize());
                await BroadcastToNearbyPlayersAsync(new PartnerWalkPacket(targetDigimonX, targetDigimonY, PartnerHandle).Serialize());
                */

                // Guardamos en la Base de Datos para que al reloguear aparezcas aquí
                if (_account != null && _currentCharacter != null)
                {
                    Console.WriteLine($"[DB] Actualizando en Mongo -> AccountId: {_account.AccountId}, Slot: {_currentCharacter.Slot}");
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