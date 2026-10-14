namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class IgnorePacketHandler(string? logMessage = null) : IPacketHandler
{
    public Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        if (!string.IsNullOrEmpty(logMessage))
        {
            Console.WriteLine(logMessage);
        }
        return Task.CompletedTask;
    }
}
