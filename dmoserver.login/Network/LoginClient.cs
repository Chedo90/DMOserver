namespace dmoserver.login.Network;

using System.Net.Sockets;
using dmoserver.database;
using dmoserver.login.Packet;

/// <summary>
/// Sesión de un cliente conectado al Login Server.
/// Flujo: Handshake (-1/-2) -> Login (3301) -> ServerList (1701) -> Selección (1702) -> pase al Character Server (901).
/// </summary>
public sealed class LoginClient
{
    // --- Opcodes ---
    private const short OpcodeHandshakeRequest = -1;
    private const short OpcodeHandshakeResponse = -2;
    private const short OpcodeKeepConnection = -3;
    private const short OpcodeLoginRequest = 3301;
    private const short OpcodeServerListRequest = 1701;
    private const short OpcodeServerSelect = 1702;

    private const short HandshakeDegree = 32321;
    private const byte LoginFailIncorrectPassword = 73; // LoginFailReasonEnum.IncorrectPassword

    // --- Límites de seguridad ---
    private const int MinPacketLength = 4;
    private const int MaxPacketLength = 4096;
    private const int MaxFailedLogins = 5;
    private const int MaxUsernameLength = 32;
    private const int MaxPasswordLength = 64;

    // --- Destino tras elegir servidor ---
    private const string CharacterServerIp = "127.0.0.1";
    private const int CharacterServerPort = 7030;

    private readonly TcpClient _socket;
    private readonly NetworkStream _stream;
    private readonly MongoDbContext _db;
    private readonly string _endPoint;
    private readonly byte[] _lengthBuffer = new byte[2];

    private short _clientHandshake;
    private bool _handshakeCompleted;
    private int _failedLogins;
    private GameAccount? _account;

    public LoginClient(TcpClient socket, MongoDbContext db)
    {
        _socket = socket;
        _db = db;
        _stream = socket.GetStream();
        _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    }

    public async Task StartAsync()
    {
        _socket.NoDelay = true;
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        Log(ConsoleColor.Green, $"\n[+] [Login] Conexión entrante desde: {_endPoint}");

        try
        {
            // Saludo inicial: el servidor envía la clave de handshake (-1)
            _clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFF);

            var hello = new PacketWriter(OpcodeHandshakeRequest);
            hello.WriteShort(_clientHandshake);
            await SendAsync(hello.Build());

            while (true)
            {
                byte[] data = await ReadPacketAsync();
                var packet = new LoginPacketReader(data, data.Length);

                if (!await ProcessPacketAsync(packet))
                    break;
            }
        }
        catch (EndOfStreamException)
        {
            // El cliente cerró la conexión: es un cierre normal.
        }
        catch (Exception ex)
        {
            Log(ConsoleColor.Red, $"[-] [Login] Error con {_endPoint}: {ex.Message}");
        }
        finally
        {
            _socket.Close();
            Log(ConsoleColor.DarkGray, $"[-] [Login] Sesión cerrada con {_endPoint}");
        }
    }

    /// <summary>
    /// Lee exactamente un paquete completo (longitud en los 2 primeros bytes),
    /// sin perder ni mezclar datos aunque TCP los parta o los junte.
    /// </summary>
    private async Task<byte[]> ReadPacketAsync()
    {
        await _stream.ReadExactlyAsync(_lengthBuffer.AsMemory(0, 2));

        short length = BitConverter.ToInt16(_lengthBuffer, 0);
        if (length < MinPacketLength || length > MaxPacketLength)
            throw new InvalidDataException($"Longitud de paquete inválida: {length}");

        byte[] data = new byte[length];
        _lengthBuffer.CopyTo(data, 0);
        await _stream.ReadExactlyAsync(data.AsMemory(2, length - 2));
        return data;
    }

    /// <returns>true para seguir con la sesión, false para cerrarla.</returns>
    private async Task<bool> ProcessPacketAsync(LoginPacketReader packet)
    {
        // El primer paquete tras el saludo es la confirmación del handshake.
        if (!_handshakeCompleted)
        {
            if (packet.Type != OpcodeHandshakeRequest)
                Log(ConsoleColor.Yellow, $"[!] [Login] Se esperaba el handshake (-1) y llegó el opcode {packet.Type}; se continúa igualmente.");

            await CompleteHandshakeAsync();
            return true;
        }

        switch (packet.Type)
        {
            case OpcodeLoginRequest:
                return await HandleLoginAsync(packet);

            case OpcodeServerListRequest:
                return await HandleServerListAsync();

            case OpcodeServerSelect:
                return await HandleServerSelectAsync();

            case OpcodeKeepConnection:
                return true;

            default:
                Log(ConsoleColor.Yellow, $"[<-] [Login] Opcode no implementado: {packet.Type} ({packet.Length} bytes)");
                return true;
        }
    }

    private async Task CompleteHandshakeAsync()
    {
        var writer = new PacketWriter(OpcodeHandshakeResponse);
        writer.WriteShort((short)(_clientHandshake ^ HandshakeDegree));
        writer.WriteUInt((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await SendAsync(writer.Build());

        _handshakeCompleted = true;
    }

    private async Task<bool> HandleLoginAsync(LoginPacketReader packet)
    {
        // Cuerpo: 5 bytes de cabecera propia del cliente, luego usuario y contraseña (longitud + texto + 0)
        packet.Skip(5);
        string username = packet.ReadString();
        string password = packet.ReadString();

        GameAccount? account = null;

        if (IsValidCredential(username, MaxUsernameLength) && IsValidCredential(password, MaxPasswordLength))
        {
            Log(ConsoleColor.Cyan, $"\n[+] [Login] Intento de login: '{username}'");
            account = await _db.AuthenticateOrRegisterAsync(username, password);
        }
        else
        {
            Log(ConsoleColor.Red, $"[-] [Login] Credenciales con formato inválido desde {_endPoint}.");
        }

        if (account == null)
        {
            // Login fallido nativo. No cerramos el socket para evitar el cartel de desconexión del cliente.
            var fail = new PacketWriter(OpcodeLoginRequest);
            fail.WriteByte(LoginFailIncorrectPassword);
            fail.WriteByte(39);
            fail.WriteByte(0);
            fail.WriteByte(0);
            fail.WriteByte(0);
            await SendAsync(fail.Build());

            _failedLogins++;
            if (_failedLogins >= MaxFailedLogins)
            {
                Log(ConsoleColor.Red, $"[-] [Login] Demasiados intentos fallidos desde {_endPoint}. Cerrando conexión.");
                return false;
            }

            Log(ConsoleColor.Red, $"[-] [Login] Fallo de credenciales ({_failedLogins}/{MaxFailedLogins}). Se mantiene el socket para reintento.");
            return true;
        }

        _account = account;
        _failedLogins = 0;

        var success = new PacketWriter(OpcodeLoginRequest);
        success.WriteInt(0);
        success.WriteByte(1);
        await SendAsync(success.Build());

        Log(ConsoleColor.Cyan, $"[+] [Login] Autenticación correcta: {account.Username} (ID: {account.AccountId})");
        return true;
    }

    private async Task<bool> HandleServerListAsync()
    {
        if (_account == null)
        {
            Log(ConsoleColor.Red, "[-] [Login] Intento de pedir ServerList sin sesión autenticada.");
            return false;
        }

        Log(ConsoleColor.Cyan, $"[+] [Login] Enviando ServerList a {_account.Username} (ID: {_account.AccountId})...");
        await SendAsync(ServerListPacket.Create());
        return true;
    }

    private async Task<bool> HandleServerSelectAsync()
    {
        if (_account == null)
        {
            Log(ConsoleColor.Red, "[-] [Login] Intento de seleccionar servidor sin sesión autenticada.");
            return false;
        }

        Log(ConsoleColor.Magenta, $"[+] [Login] {_account.Username} seleccionó servidor. Enviando pase al Character Server (Opcode 901)...");
        await SendAsync(ConnectCharacterServerPacket.Create(_account.AccountId, CharacterServerIp, CharacterServerPort));
        return true;
    }

    private static bool IsValidCredential(string value, int maxLength) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= maxLength
        && value.All(c => c >= 0x20 && c != 0x7F);

    private async Task SendAsync(byte[] data)
    {
        await _stream.WriteAsync(data);
        await _stream.FlushAsync();
    }

    private static void Log(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}