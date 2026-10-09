namespace dmoserver.map.Network;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Buffers;
// using dmoserver.game.Packets; // Referencia a tu librería de lectura/escritura de paquetes

public sealed class MapClient
{
    public TcpClient Socket { get; }
    private readonly NetworkStream _stream;
    private readonly string _endPoint;

    public MapClient(TcpClient socket)
    {
        Socket = socket;
        Socket.NoDelay = true;
        _stream = socket.GetStream();
        _endPoint = socket.Client.RemoteEndPoint?.ToString() ?? "Desconocido";
    }

    public async Task StartAsync()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[+] Cliente conectado al mapa desde: {_endPoint}");
        Console.ResetColor();

        try
        {
            // El bucle infinito de lectura (ReadAtLeastAsyncCore) que ya conoces
            byte[] lengthBuffer = new byte[2];

            while (Socket.Connected)
            {
                try
                {
                    await _stream.ReadExactlyAsync(lengthBuffer.AsMemory(0, 2));
                }
                catch (EndOfStreamException)
                {
                    break; // Desconexión limpia
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

                    // Aquí procesaremos los Opcodes más adelante
                    // var packet = new GamePacketReader(packetData, packetLength);
                    // await ProcessPacketAsync(packet);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(packetData);
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"[-] Error de red con {_endPoint}: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            Socket.Close();
            Console.WriteLine($"[-] Cliente {_endPoint} desconectado del MapServer.");
        }
    }
}