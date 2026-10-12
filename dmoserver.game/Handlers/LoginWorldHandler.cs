namespace dmoserver.game.Handlers;

using System;
using System.Linq;
using System.Threading.Tasks;
using dmoserver.database;
using dmoserver.game.Entities;
using dmoserver.game.Managers;
using dmoserver.game.Network;
using dmoserver.game.Packets;

public static class LoginWorldHandler
{
    private static readonly MongoDbContext Db = new();

    public static async Task HandleInitialInformationAsync(GameClient client, GamePacketReader packet)
    {
        var player = client.Player;

        // 1. Cargar la cuenta desde MongoDB si aún no está en memoria
        if (player.Account == null)
        {
            uint accountIdToSearch = player.AccountId > 0 ? player.AccountId : 1;
            player.Account = await Db.GetAccountByIdAsync(accountIdToSearch)
                             ?? await Db.GetOrCreateAccountAsync(accountIdToSearch);

            if (player.Account != null)
            {
                player.AccountId = player.Account.AccountId;
            }
        }

        if (player.Account == null) return;

        // 2. Refrescar datos por si hubo cambios recientes
        var freshAccount = await Db.GetAccountByIdAsync(player.Account.AccountId);
        if (freshAccount != null) player.Account = freshAccount;

        player.Account.Characters ??= [];
        if (player.Account.Characters.Count == 0) return;

        // 3. Diagnóstico y Selección del personaje
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[LoginWorld] AccountId: {player.Account.AccountId} | LastPlayedSlot en BD: {player.Account.LastPlayedSlot}");
        foreach (var c in player.Account.Characters)
        {
            Console.WriteLine($"  -> Personaje en BD: '{c.Name}' en Slot {c.Slot}");
        }
        Console.ResetColor();

        // 3.1 Prioridad por nombre si fue especificado explícitamente
        if (!string.IsNullOrEmpty(player.SelectedCharacterName))
        {
            player.Character = player.Account.Characters.FirstOrDefault(c =>
                c.Name.Equals(player.SelectedCharacterName, StringComparison.OrdinalIgnoreCase));
        }

        // 3.2 Buscar por el slot seleccionado guardado en BD
        player.Character ??= player.Account.Characters.FirstOrDefault(c => c.Slot == player.Account.LastPlayedSlot);

        // 3.3 Si falla, tomar el primer slot disponible ordenado ascendentemente (slot 0) en lugar del último
        player.Character ??= player.Account.Characters.OrderBy(c => c.Slot).FirstOrDefault();

        if (player.Character == null)
        {
            Console.WriteLine("[LoginWorld] ERROR: No se encontró ningún personaje para esta cuenta.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[LoginWorld] SELECCIONADO CON ÉXITO: '{player.Character.Name}' (Slot {player.Character.Slot})");
        Console.ResetColor();

        // 4. Asegurar coordenadas válidas
        if (player.Character.Location == null || (player.Character.Location.X == 0 && player.Character.Location.Y == 0))
        {
            player.Character.Location = new CharacterLocation
            {
                MapId = player.Character.Location?.MapId ?? 1,
                X = 27993,
                Y = 29095,
                Z = 0f
            };
        }

        player.X = player.Character.Location.X;
        player.Y = player.Character.Location.Y;

        if (player.Character.Partner == null) return;

        int mapId = player.Character.Location.MapId;

        // 5. Vincular al mapa
        client.SetCurrentMap(MapManager.GetOrCreateMap(mapId));
        client.CurrentMap?.Players.TryAdd(client.EndPoint, client);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[Login] Cargando Tamer: {player.Character.Name} en Mapa {mapId} (Jugadores en mapa: {client.CurrentMap?.Players.Count})");
        Console.ResetColor();

        // 6. Enviar paquetes de inicio al cliente
        byte[] initialInfo = new InitialInfoPacket(
            player.Character.Name,
            player.Character.Partner.Name,
            player.Character.Model,
            player.Character.Partner.Model,
            player.TamerHandle,
            player.PartnerHandle,
            player.Character.Location.X,
            player.Character.Location.Y
        ).Serialize();

        await client.SendAsync(initialInfo);

        var tamerStatus = new UpdateStatusPacket(player.TamerHandle, 1000, 1000, 500, 500);
        await client.SendAsync(tamerStatus.Serialize());

        var partnerStatus = new UpdateStatusPacket(player.PartnerHandle, 1000, 1000, 500, 500);
        await client.SendAsync(partnerStatus.Serialize());
    }
}