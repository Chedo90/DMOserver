namespace dmoserver.map;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using dmoserver.map.Network;

class Program
{
    const int port = 7035;

    static async Task Main(string[] args)
    {
        Console.Title = "DMO Server - MapServer";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Iniciando MapServer en el puerto {port}...");
        Console.ResetColor();

        // Aquí inicializaremos la base de datos y los gestores de mapas más adelante

        TcpListener listener = new(IPAddress.Any, port);
        listener.Start();

        Console.WriteLine("MapServer escuchando conexiones entrantes...");

        while (true)
        {
            try
            {
                TcpClient socket = await listener.AcceptTcpClientAsync();
                
                // Instanciamos el cliente dedicado a este mapa y lo arrancamos en segundo plano
                MapClient client = new(socket);
                _ = client.StartAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error aceptando conexión: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}