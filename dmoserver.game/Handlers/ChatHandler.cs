namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets;

public static class ChatHandler
{
    public static async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        string message = packet.ReadString();
        string senderName = client.CurrentCharacter?.Name ?? "Tamer";
        
        // ¡Usamos el Handle dinámico del personaje conectado!
        uint tamerHandle = client.TamerHandle; 

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[Chat recibido] {senderName}: {message}");
        Console.ResetColor();

        bool isCommand = await CommandProcessor.ExecuteAsync(client, message);
        if (isCommand) return;

        // Estructura exacta calcada del hex dump (Idéntica a nuestra "Prueba B")
        using var w = new PacketWriter(1006);
        w.WriteByte(7);              // ChatType Normal
        w.WriteByte(1);              // Flag
        w.WriteUInt(tamerHandle);    // Handle dinámico del Tamer
        w.WriteString(message);      // Texto
        w.WriteByte(0);              // Byte nulo final exigido por el cliente
        
        await client.SendAsync(w.Serialize());
    }
}