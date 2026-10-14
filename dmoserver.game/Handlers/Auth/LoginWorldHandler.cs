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
        int targetMapId = (player.Character.Location != null && player.Character.Location.MapId > 0)
            ? player.Character.Location.MapId
            : 105;

        if (player.Character.Location == null || (player.Character.Location.X == 0 && player.Character.Location.Y == 0))
        {
            player.Character.Location = new CharacterLocation
            {
                MapId = targetMapId,
                X = 15016,
                Y = 10378,
                Z = -2.724508f
            };
        }
        else
        {
            player.Character.Location.MapId = targetMapId;
        }

        player.X = player.Character.Location.X;
        player.Y = player.Character.Location.Y;
        player.Z = player.Character.Location.Z;
        player.PartnerX = player.X;
        player.PartnerY = player.Y;
        player.PartnerZ = player.Z;

        var partnerDoc = player.Character.Partner;
        if (partnerDoc == null) return;

        // Sincronizar nivel y exp del Tamer
        player.TamerLevel = player.Character.Level;
        player.TamerCurrentExp = player.Character.Exp;

        // Cargar plantilla base de la especie: buscar en digimon_templates (Digimon_List.xml)
        var digimonTemplate = await Db.GetDigimonTemplateAsync(partnerDoc.Model);
        player.PartnerStats = dmoserver.game.Entities.Stats.DigimonStats.FromDigimonTemplate(
            digimonTemplate,
            partnerDoc.Level,
            partnerDoc.Size,
            partnerDoc.Friendship
        );
        player.PartnerLevel = partnerDoc.Level;
        player.CurrentExp = partnerDoc.Exp;

        // Velocidad sincronizada con el Tamer (600) para un seguimiento natural
        player.PartnerStats.MoveSpeed = 600;

        int mapId = player.Character.Location.MapId;

        // 5. Vincular al mapa
        client.SetCurrentMap(MapManager.GetOrCreateMap(mapId));
        client.CurrentMap?.Players.TryAdd(client.EndPoint, client);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[Login] Cargando Tamer: {player.Character.Name} | Partner: {partnerDoc.Name} (Modelo {partnerDoc.Model}, Nivel {player.PartnerLevel}, Size {partnerDoc.Size/100f}%, Amistad {player.PartnerStats.Friendship})");
        Console.WriteLine($"        Stats Partner -> HP: {player.PartnerStats.MaxHp} | AT: {player.PartnerStats.Attack} | DE: {player.PartnerStats.Defense} | AS: {player.PartnerStats.AttackSpeed}ms | MS: {player.PartnerStats.MoveSpeed} | CT: {player.PartnerStats.CriticalRate:F1}% | EV: {player.PartnerStats.EvasionRate:F1}%");
        Console.ResetColor();

        // 6. Enviar paquetes de inicio al cliente
        byte[] initialInfo = new InitialInfoPacket(
            player,
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

        // Actualizar hoja completa de estadísticas de Tamer y Digimon (Opcode 1043)
        await client.SendAsync(new UpdateFullStatusPacket(player).Serialize());

        var tamerStatus = new UpdateStatusPacket(player.TamerHandle, 1000, 1000, 500, 500);
        await client.SendAsync(tamerStatus.Serialize());

        var partnerStatus = new UpdateStatusPacket(
            player.PartnerHandle,
            player.PartnerStats.CurrentHp,
            player.PartnerStats.MaxHp,
            player.PartnerStats.CurrentDs,
            player.PartnerStats.MaxDs
        );
        await client.SendAsync(partnerStatus.Serialize());

        await client.SendAsync(new UpdateMovementSpeedPacket(player.TamerHandle, player.PartnerHandle, 600, (short)player.PartnerStats.MoveSpeed).Serialize());
    }
}