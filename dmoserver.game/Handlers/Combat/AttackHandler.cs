namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class AttackHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        Console.WriteLine($"[{packet.Type} RAW] {packet.ToHexString()}");
        await CombatHandler.HandleAttackRequestAsync(client, packet);
    }
}
