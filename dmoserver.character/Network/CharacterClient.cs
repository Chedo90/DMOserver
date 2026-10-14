using System.Net.Sockets;
using dmoserver.character.Enums;
using dmoserver.character.Packets;
using dmoserver.database;

namespace dmoserver.character.Network;

/// <summary>
/// Sesión de un cliente conectado al Character Server.
/// Sustituye a la lógica que vivía en Program.cs y al antiguo CharacterPacketProcessor.
/// </summary>
public sealed class CharacterClient
{
    private const short HandshakeDegree = 32321;

    // --- Límites de seguridad ---
    private const int MinPacketLength = 4;
    private const int MaxPacketLength = 4096;
    private const byte MaxSlot = 4; // slots válidos: 0..4

    // --- Datos de partida ---
    private const short DefaultStartMapId = 105; // mapa inicial de los personajes nuevos (D-Terminal / DATS)
    private const string GameServerIp = "127.0.0.1";
    private const string GameServerPort = "7031";

    // Posibles offsets del slot en el paquete 1305 (se ignoran los 2 últimos bytes: checksum)
    private static readonly int[] SlotOffsetCandidates = [8, 4, 7];

    private readonly TcpClient _socket;
    private readonly NetworkStream _stream;
    private readonly MongoDbContext _db;
    private readonly string _endPoint;
    private readonly byte[] _lengthBuffer = new byte[2];

    private short _clientHandshake;
    private uint _accountId;

    private bool IsAuthenticated => _accountId != 0;

    public CharacterClient(TcpClient socket, MongoDbContext db)
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

        Log(ConsoleColor.Green, $"\n[+] Cliente conectado al Character Server desde: {_endPoint}");

        try
        {
            // 1. EVENTO ONCONNECT (Opcode 65535 = -1)
            _clientHandshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFF);
            await SendAsync(new OnConnectEventConnectionPacket(_clientHandshake).Serialize());
            Log(ConsoleColor.Cyan, $"[->] OnConnectEventConnectionPacket (65535) enviado. Handshake: {_clientHandshake}");

            while (true)
            {
                byte[] data = await ReadPacketAsync();
                var packet = new CharacterPacketReader(data);

                Log(ConsoleColor.Yellow, $"[<-] Paquete recibido: {packet.Enum} (Opcode: {packet.Type}, Tamaño: {packet.Length})");

                if (!await ProcessPacketAsync(packet))
                    break;
            }
        }
        catch (EndOfStreamException)
        {
            // El cliente cerró la conexión: cierre normal.
        }
        catch (Exception ex)
        {
            Log(ConsoleColor.DarkGray, $"[-] Sesión finalizada con {_endPoint}: {ex.Message}");
        }
        finally
        {
            _socket.Close();
            Log(ConsoleColor.DarkGray, $"[-] Conexión cerrada: {_endPoint}");
        }
    }

    /// <summary>
    /// Lee exactamente un paquete completo validando su longitud.
    /// Un paquete incompleto o con longitud absurda cierra la conexión (nunca se procesa a medias).
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
    private async Task<bool> ProcessPacketAsync(CharacterPacketReader packet)
    {
        switch (packet.Enum)
        {
            case CharacterServerPacketEnum.Connection:            // -1
                return await HandleConnectionAsync();

            case CharacterServerPacketEnum.KeepConnection:        // -3
                return true;

            case CharacterServerPacketEnum.RequestCharacters:     // 1706
                return await HandleRequestCharactersAsync(packet);

            case CharacterServerPacketEnum.CheckNameDuplicity:    // 1302
                return await HandleCheckNameAsync(packet);

            case CharacterServerPacketEnum.CreateCharacter:       // 1303
                return await HandleCreateCharacterAsync(packet);

            case CharacterServerPacketEnum.GetCharacterPosition:  // 1305
                return await HandleSelectCharacterAsync(packet);

            case CharacterServerPacketEnum.ConnectGameServer:     // 1703
                return await HandleConnectGameServerAsync();

            default:
                Log(ConsoleColor.Gray, $"[!] Paquete no mapeado: {packet.Enum} ({packet.Type})");
                return true;
        }
    }

    // ------------------------------------------------------------------
    // Handlers
    // ------------------------------------------------------------------

    private async Task<bool> HandleConnectionAsync()
    {
        short handshake = (short)(_clientHandshake ^ HandshakeDegree);
        uint timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await SendAsync(new ConnectionPacket(handshake, timestamp).Serialize());
        Log(ConsoleColor.Cyan, "[->] ConnectionPacket (-2) enviado confirmando el Handshake.");
        return true;
    }

    private async Task<bool> HandleRequestCharactersAsync(CharacterPacketReader packet)
    {
        packet.Seek(8);
        uint accountId = packet.ReadUInt();

        Log(ConsoleColor.Green, $"[+] El cliente solicita la lista de personajes para el AccountId: {accountId}");

        var account = accountId == 0 ? null : await _db.GetAccountByIdAsync(accountId);
        if (account == null)
            return Deny($"La cuenta {accountId} no existe. Cerrando conexión.");

        _accountId = accountId;

        await SendAsync(new CharacterListPacket(account.Characters).Serialize());
        Log(ConsoleColor.Cyan, $"[->] CharacterListPacket (1301) enviado con {account.Characters.Count} personaje(s).");
        return true;
    }

    private async Task<bool> HandleCheckNameAsync(CharacterPacketReader packet)
    {
        if (!IsAuthenticated)
            return Deny("1302 recibido sin haber pedido antes la lista de personajes.");

        string tamerName = packet.ReadString();
        Log(ConsoleColor.Yellow, $"[->] Verificando disponibilidad para el nombre de Tamer: '{tamerName}'");

        bool isAvailable = IsValidTamerName(tamerName) && !await _db.IsTamerNameTakenAsync(tamerName);

        await SendAsync(new AvailableNamePacket(isAvailable).Serialize());
        Log(ConsoleColor.Cyan, $"[->] AvailableNamePacket (1302) enviado: {(isAvailable ? "Disponible" : "Ocupado / inválido")}");
        return true;
    }

    private async Task<bool> HandleCreateCharacterAsync(CharacterPacketReader packet)
    {
        if (!IsAuthenticated)
            return Deny("1303 recibido sin cuenta autenticada.");

        // 1. Tamer
        byte slot = packet.ReadByte();
        int tamerModel = packet.ReadInt();
        string tamerName = packet.ReadZString();

        // 2. Digimon (salto al offset 42 en el paquete entrante)
        packet.Seek(42);
        int digimonModel = packet.ReadInt();
        string digimonName = packet.ReadZString();

        Log(ConsoleColor.Green, $"[+] Creando personaje para AccountId {_accountId}:");
        Log(ConsoleColor.Green, $"    Tamer: '{tamerName}' (Modelo: {tamerModel}) en slot {slot}");
        Log(ConsoleColor.Green, $"    Digimon: '{digimonName}' (Modelo: {digimonModel})");

        // 3. Validaciones
        if (slot > MaxSlot)
            return Deny($"Slot inválido: {slot}.");

        if (!IsValidTamerName(tamerName) || !IsValidDigimonName(digimonName))
            return Deny("Nombre de tamer o de digimon con formato inválido.");

        // 4. Reservar el nombre (atómico: evita duplicados aunque dos jugadores lo intenten a la vez)
        if (!await _db.TryReserveTamerNameAsync(tamerName, _accountId))
        {
            Log(ConsoleColor.Yellow, $"[-] El nombre '{tamerName}' ya está en uso. Creación rechazada.");
            await SendAsync(new AvailableNamePacket(false).Serialize());
            return true;
        }

        // 5. Añadir el personaje de forma atómica (solo si el slot está libre)
        var character = new CharacterDocument
        {
            Slot = slot,
            Name = tamerName,
            Model = tamerModel,
            Level = 1,
            Location = new CharacterLocation 
            { 
                MapId = DefaultStartMapId, 
                X = 15016, 
                Y = 10378, 
                Z = -2.724508f 
            },
            Partner = new PartnerDigimonDocument
            {
                Name = digimonName,
                Model = digimonModel,
                Level = 1,
                Size = 10000
            }
        };

        bool added;
        try
        {
            added = await _db.TryAddCharacterAsync(_accountId, character);
        }
        catch
        {
            await _db.ReleaseTamerNameAsync(tamerName);
            throw;
        }

        if (!added)
        {
            await _db.ReleaseTamerNameAsync(tamerName);
            return Deny($"El slot {slot} ya está ocupado o la cuenta no existe. Creación rechazada.");
        }

        Log(ConsoleColor.Magenta, $"[✓] Personaje '{tamerName}' guardado en MongoDB para la cuenta {_accountId}.");

        // 6. Paquete de respuesta 1306
        short handshake = (short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFF);

        byte[] createdPacket = new CharacterCreatedPacket(
            slot,
            tamerModel,
            tamerName,
            digimonModel,
            digimonName,
            handshake,
            mapId: DefaultStartMapId
        ).Serialize();

        await SendAsync(createdPacket);
        Log(ConsoleColor.Cyan, "[->] CharacterCreatedPacket (1306) enviado. Creación confirmada.");
        return true;
    }

    private async Task<bool> HandleSelectCharacterAsync(CharacterPacketReader packet)
    {
        if (!IsAuthenticated)
            return Deny("1305 recibido sin cuenta autenticada.");

        // Volcado de depuración de los bytes crudos
        byte[] raw = packet.Data;
        Log(ConsoleColor.Gray, $"[1305 DUMP] {BitConverter.ToString(raw)}");

        // Los 2 últimos bytes son el checksum: nunca pueden ser el slot.
        int bodyEnd = raw.Length - 2;
        int slot = -1;

        foreach (int index in SlotOffsetCandidates)
        {
            if (index < bodyEnd && raw[index] <= MaxSlot)
            {
                slot = raw[index];
                break;
            }
        }

        if (slot < 0)
            return Deny("No se pudo determinar un slot válido en el paquete 1305.");

        var account = await _db.GetAccountByIdAsync(_accountId);
        var character = account?.Characters.FirstOrDefault(c => c.Slot == slot);

        if (character == null)
            return Deny($"El slot {slot} no tiene personaje en la cuenta {_accountId}.");

        Log(ConsoleColor.Green, $"[+] [LOBBY] Personaje seleccionado en SLOT: {slot}");

        await _db.SetLastPlayedSlotAsync(_accountId, (byte)slot);
        Log(ConsoleColor.Cyan, $"[✓] MongoDB actualizado: LastPlayedSlot = {slot} para cuenta {_accountId}");

        short mapId = (short)character.Location.MapId;
        await SendAsync(new ConnectGameServerInfoPacket(GameServerIp, GameServerPort, mapId).Serialize());
        return true;
    }

    private async Task<bool> HandleConnectGameServerAsync()
    {
        if (!IsAuthenticated)
            return Deny("1703 recibido sin cuenta autenticada.");

        Log(ConsoleColor.Green, "[+] Cliente solicita autorización para saltar al Game Server.");

        await SendAsync(new ConnectGameServerPacket().Serialize());
        Log(ConsoleColor.Cyan, "[->] ConnectGameServerPacket (1703) enviado. ¡Transferencia al Game Server autorizada!");
        return true;
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    private static bool IsValidTamerName(string name) =>
        name.Length is >= 2 and <= 20
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static bool IsValidDigimonName(string name) =>
        name.Length is >= 1 and <= 20
        && name.All(c => c >= 0x20 && c <= 0x7E);

    /// <summary>Registra el motivo del rechazo y devuelve false para cerrar la sesión.</summary>
    private bool Deny(string reason)
    {
        Log(ConsoleColor.Red, $"[-] [{_endPoint}] {reason}");
        return false;
    }

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