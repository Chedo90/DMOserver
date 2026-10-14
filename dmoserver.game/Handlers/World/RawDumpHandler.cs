namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class RawDumpHandler : IPacketHandler
{
    public Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        Console.WriteLine($"[{packet.Type} RAW] {packet.ToHexString()}");
        return Task.CompletedTask;
    }
}
