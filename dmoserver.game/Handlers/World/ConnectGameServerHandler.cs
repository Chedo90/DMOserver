namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class ConnectGameServerHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[+] [GameServer] Cliente solicita autorización de conexión/salto de mapa (1703). Respondiendo 1703...");
        Console.ResetColor();

        // Paquete de confirmación 1703 (ConnectGameServer autorizada)
        using var writer = new PacketWriter(1703);
        await client.SendAsync(writer.Serialize());
    }
}
