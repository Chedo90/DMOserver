namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets;

public static class ChatHandler
{
    public static async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        string message = packet.ReadZString();
        string senderName = client.CurrentCharacter?.Name ?? "Tamer";

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[Chat] {senderName}: {message}");
        Console.ResetColor();

        // 1. Procesador de comandos GM
        bool isCommand = await CommandProcessor.ExecuteAsync(client, message);
        if (isCommand)
            return;

        // 2. Chat normal: Usamos el Handle para que salga el bocadillo
        uint tamerHandle = client.CurrentCharacter != null ? (uint)(100000 + client.CurrentCharacter.Slot) : 100000;
        
        byte[] chatResponse = new ChatMessagePacket(message, tamerHandle, ChatType.Normal).Serialize();
        await client.SendAsync(chatResponse);
    }
}