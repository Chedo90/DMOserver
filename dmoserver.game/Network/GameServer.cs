namespace dmoserver.game.Network;

using System.Net;
using System.Net.Sockets;

// Constructor Primario de C# 12+ (port se convierte en variable global para la clase)
public sealed class GameServer(int port)
{
    private readonly TcpListener _listener = new(IPAddress.Any, port);

    public async Task StartAsync()
    {
        _listener.Start();

        Console.Title = "DMO Custom Game Server";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"=== Game Server activo y escuchando en el puerto {port} ===");
        Console.WriteLine("Esperando que el cliente entre desde la pantalla de carga...\n");
        Console.ResetColor();

        while (true)
        {
            TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
            var client = new GameClient(tcpClient);
            _ = Task.Run(() => client.StartAsync());
        }
    }
}