namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class CharacterSelectHandler : IPacketHandler
{
    public Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        client.Player.SelectedCharacterName = packet.ReadString();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Selección] El cliente eligió al Tamer: {client.Player.SelectedCharacterName}");
        Console.ResetColor();
        return Task.CompletedTask;
    }
}
