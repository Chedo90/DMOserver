namespace dmoserver.game.Network;

using System.Buffers;
using System.IO;
using System.Net.Sockets;
using dmoserver.game.Packets;

public sealed class GameClient(TcpClient socket)
{
    private const short HandshakeDegree = 32321;
    private const int OnConnectEventHandshakeHandler = 65535;

    public TcpClient Socket { get; } = socket;

    private readonly NetworkStream _stream = socket.GetStream();
    private readonly string _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    private short _clientHandshake;

    public async Task StartAsync()
    {
        Socket.NoDelay = true;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[+] Cliente conectado desde: {_endPoint}");
        Console.ResetColor();

        try
        {
            // 1. Handshake inicial OnConnect (Type = 65535 / -1) con Checksum oficial
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
            Socket.Close();
            Console.WriteLine($"[-] Conexión con {_endPoint} cerrada.");
        }
    }

    private async Task ProcessPacketAsync(GamePacketReader packet)
    {
        switch (packet.Type)
        {
            case -1: // Confirmación de Handshake por parte del cliente
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

            case 1706: // InitialInformation: El cliente solicita entrar
            {
                packet.Skip(4); // Saltamos los primeros 4 bytes igual que en InitialInformationPacketProcessor
                uint accountId = packet.ReadUInt();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[+] Solicitud de entrada al mapa para AccountId: {accountId}");
                Console.ResetColor();

                // EN 1706 SOLO SE ENVÍA INITIALINFO (1003)
                byte[] initialInfo = new InitialInfoPacket("Chedo", "Chedorra", 80001, 31001).Serialize();
                await SendAsync(initialInfo);

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[✓] InitialInfoPacket (1003) enviado ({initialInfo.Length} bytes).");
                Console.WriteLine("[*] Esperando respuesta del cliente (Opcode 1001)...");
                Console.ResetColor();
                break;
            }

            case 1001: // ComplementarInformation: El cliente cargó el mapa y pide el resto
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[+] Cliente confirmó carga del mapa (Opcode 1001 recibido).");
                Console.ResetColor();

                // Ahora que el mapa está listo en el cliente, spawneamos al Tamer y al Digimon
                byte[] loadTamer = new LoadTamerPacket("Chedo", "Chedorra", 80001, 31001).Serialize();
                await SendAsync(loadTamer);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[->] LoadTamerPacket (1006) enviado.");
                Console.ResetColor();
                break;
            }

            default:
            {
                if (packet.Type != 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"[!] Opcode recibido: {packet.Type} (Tamaño: {packet.Length})");
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