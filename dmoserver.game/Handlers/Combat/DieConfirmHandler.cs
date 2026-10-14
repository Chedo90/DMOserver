namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Combat;
using dmoserver.game.Packets.Digimon;

public sealed class DieConfirmHandler : IPacketHandler
{
    private const string GameServerIp = "127.0.0.1";
    private const int GameServerPort = 7031;

    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        var player = client.Player;
        if (player == null || player.Character == null) return;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✨ Resurrección] El jugador '{player.Character.Name}' confirmó la resurrección (1022).");
        Console.ResetColor();

        // 1. Restaurar vida y recursos del Digimon
        player.PartnerStats.CurrentHp = player.PartnerStats.MaxHp;
        player.PartnerStats.CurrentDs = player.PartnerStats.MaxDs;
        player.InCombat = false;
        player.CombatTargetHandle = 0;

        // 2. Coordenadas de reaparición (aldea / spawn inicial del mapa)
        int mapId = player.Character.Location?.MapId ?? 1;
        int spawnX = 27993;
        int spawnY = 29095;

        player.X = spawnX;
        player.Y = spawnY;
        player.PartnerX = spawnX;
        player.PartnerY = spawnY;

        if (player.Character.Location != null)
        {
            player.Character.Location.X = spawnX;
            player.Character.Location.Y = spawnY;
        }

        // Guardar la nueva posición en la base de datos
        if (player.Account != null)
        {
            _ = GameClient.Db.UpdatePositionAsync(player.Account.AccountId, player.Character.Slot, spawnX, spawnY, 0f);
        }

        // 3. Salir del mapa actual para forzar recarga limpia
        client.CurrentMap?.Players.TryRemove(client.EndPoint, out _);
        player.LastLoadedMapId = -1;

        // 4. Enviar MapSwapPacket (1709) para recargar mapa en el punto de reaparición
        var swapPacket = new MapSwapPacket(GameServerIp, GameServerPort, mapId, spawnX, spawnY).Serialize();
        await client.SendAsync(swapPacket);
    }
}
