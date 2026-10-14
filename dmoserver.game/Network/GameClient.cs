namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dmoserver.database;
using dmoserver.game.Managers;
using dmoserver.game.Maps;
using dmoserver.game.Entities;
using dmoserver.game.Handlers;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Entities;


public sealed class GameClient(TcpClient socket)
{
    public const short HandshakeDegree = 32321;
    private const int OnConnectEventHandshakeHandler = 65535;

    internal static readonly MongoDbContext Db = new();

    public TcpClient Socket { get; } = socket;
    public string EndPoint => _endPoint;

    private readonly NetworkStream _stream = socket.GetStream();
    private readonly string _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    private short _clientHandshake;
    public short ClientHandshake => _clientHandshake;
    public DateTime LastPositionSave { get; set; } = DateTime.MinValue;

    public PlayerEntity Player { get; } = new();
    public MapInstance? CurrentMap { get; private set; }

    public void SetCurrentMap(MapInstance map) => CurrentMap = map;

    private static string Hex(byte[] data, int length)
    {
        int len = Math.Min(length, data.Length);
        return BitConverter.ToString(data, 0, len).Replace("-", "");
    }

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
                    await _stream.ReadExactlyAsync(packetData.AsMemory(2, packetLength - 2));
                    packetData[0] = lengthBuffer[0];
                    packetData[1] = lengthBuffer[1];

                    int payloadLength = packetLength - 2;

                    if (payloadLength >= 7)
                    {
                        string headerText = Encoding.ASCII.GetString(packetData, 2, Math.Min(7, payloadLength));
                        if (headerText.Equals("DMIPASS", StringComparison.Ordinal))
                        {
                            string fullPayload = Encoding.ASCII.GetString(packetData, 2, payloadLength);
                            string[] parts = fullPayload.Split(' ');
                            if (parts.Length > 1)
                            {
                                Player.SessionToken = parts[1];
                                Player.Account = await Db.GetAccountBySessionTokenAsync(Player.SessionToken);

                                if (Player.Account != null)
                                {
                                    Player.AccountId = Player.Account.AccountId;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Auth] Sesión validada para la cuenta ID {Player.AccountId}");
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
                        Console.WriteLine($"[<-] Paquete recibido: Opcode {packet.Type} (Tamaño: {packet.Length})");
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
            Console.WriteLine($"[-] Error con {_endPoint}: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            CurrentMap?.Players.TryRemove(_endPoint, out _);

            if (Player.Account != null && Player.Character != null && Player.X > 0 && Player.Y > 0)
            {
                _ = Db.UpdatePositionAsync(Player.Account.AccountId, Player.Character.Slot, Player.X, Player.Y, 0f);
            }
            Socket.Close();
            Console.WriteLine($"[-] Conexión con {_endPoint} cerrada.");
        }
    }

    private async Task ProcessPacketAsync(GamePacketReader packet)
    {
        await PacketDispatcher.DispatchAsync(this, packet);
    }

    public async Task SendChatMessageAsync(string text)
    {
        byte[] msg = new ChatMessagePacket(Player.TamerHandle, text, 0).Serialize();
        await SendAsync(msg);
    }

    public async Task TeleportAsync(int x, int y)
    {
        Player.X = x;
        Player.Y = y;
        await SendAsync(new TamerTeleportPacket(x, y, Player.TamerHandle).Serialize());
        await SendAsync(new PartnerTeleportPacket(x, y, Player.PartnerHandle).Serialize());
    }

    public async Task SetSpeedAsync(short speed)
    {
        await SendAsync(new UpdateMovementSpeedPacket(Player.TamerHandle, Player.PartnerHandle, speed).Serialize());
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