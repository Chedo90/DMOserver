namespace dmoserver.login.Network;

using System.Buffers;
using System.Net.Sockets;
using dmoserver.database;
using dmoserver.login.Packet;

public sealed class LoginClient(TcpClient socket)
{
    private const short HandshakeDegree = 32321;
    private const int OnConnectEventHandshakeHandler = 65535;

    private static readonly MongoDbContext Db = new();

    public TcpClient Socket { get; } = socket;
    private readonly NetworkStream _stream = socket.GetStream();
    private readonly string _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    private short _clientHandshake;
    private GameAccount? _account;

    public async Task StartAsync()
    {
        Socket.NoDelay = true;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[+] [Login] Conexión entrante desde: {_endPoint}");
        Console.ResetColor();

        try
        {
            // Handshake inicial
            _clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler);

            var onConnectWriter = new PacketWriter(unchecked((short)65535));
            onConnectWriter.WriteShort(_clientHandshake);
            await SendAsync(onConnectWriter.Build());

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

                    var packet = new LoginPacketReader(packetData, packetLength);
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
            Console.WriteLine($"[-] [Login] Error con {_endPoint}: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            Socket.Close();
            Console.WriteLine($"[-] [Login] Sesión cerrada con {_endPoint}");
        }
    }

    private async Task ProcessPacketAsync(LoginPacketReader packet)
    {
        switch (packet.Type)
        {
            case -1: // Confirmación de Handshake
            {
                var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var handshake = (short)(_clientHandshake ^ HandshakeDegree);

                var writer = new PacketWriter(-2);
                writer.WriteShort(handshake);
                writer.WriteUInt(handshakeTimestamp);
                await SendAsync(writer.Build());
                break;
            }

        case 1301: // Client Login Request
{
    string username = packet.ReadString();
    string password = packet.ReadString();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[Login] Intento de acceso: Usuario='{username}', Pass='{password}'");
    Console.ResetColor();

    // 1. Validar contra MongoDB
    _account = await Db.AuthenticateOrRegisterAsync(username, password);

    // 2. Si las credenciales NO coinciden (contraseña incorrecta)
    if (_account == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[-] Acceso denegado para '{username}'. Contraseña inválida.");
        Console.ResetColor();

        // Opcional: enviar paquete de error si lo tienes implementado, o cerrar socket:
        Socket.Close();
        return;
    }

    // 3. SOLO si la autenticación fue correcta, enviamos los servidores
    await SendAsync(ServerListPacket.Create());
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[+] Autenticación exitosa. ServerList enviado a {username} (ID: {_account.AccountId})");
    Console.ResetColor();
    break;
}

            case 1702: // El usuario selecciona un servidor de la lista
            {
                if (_account != null)
                {
                    // Redirige al Character Server (puerto 7030 o el que uses)
                    await SendAsync(ConnectCharacterServerPacket.Create(_account.AccountId, "127.0.0.1", 7030));

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Login] Redirigiendo a AccountId {_account.AccountId} al Character Server...");
                    Console.ResetColor();
                }
                break;
            }

            default:
            {
                if (packet.Type != 0 && packet.Type != -3)
                {
                    Console.WriteLine($"[Login] Opcode recibido sin procesar: {packet.Type} (Longitud: {packet.Length})");
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