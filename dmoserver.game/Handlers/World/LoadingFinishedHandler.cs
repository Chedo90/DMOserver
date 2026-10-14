namespace dmoserver.game.Handlers;

using System.Threading.Tasks;
using dmoserver.game.Managers;
using dmoserver.game.Network;
using dmoserver.game.Packets;
using dmoserver.game.Packets.Entities;

public sealed class LoadingFinishedHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        var player = client.Player;
        if (player.Character != null && player.LastLoadedMapId != player.Character.Location.MapId)
        {
            int mapId = player.Character.Location.MapId;

            var currentMap = client.CurrentMap ?? MapManager.GetOrCreateMap(mapId);
            client.SetCurrentMap(currentMap);
            currentMap.Players.TryAdd(client.EndPoint, client);

            await currentMap.LoadSpawnsAsync(GameClient.Db);

            foreach (var mob in currentMap.Monsters.Values)
            {
                await client.SendAsync(new MobSpawnPacket(
                    mob.Handle, (int)mob.ModelId, mob.X, mob.Y, (byte)mob.Stats.Level).Serialize());

                await client.SendAsync(new MobStatePacket(mob.Handle).Serialize());
            }

            // Sincronizar hoja completa de estadísticas de Tamer y Partner (Opcode 1043)
            await client.SendAsync(new dmoserver.game.Packets.UpdateFullStatusPacket(player).Serialize());

            // Sincronizar barras de estado de Tamer y Partner (Opcode 1023)
            var tamerStatus = new dmoserver.game.Packets.UpdateStatusPacket(player.TamerHandle, 1000, 1000, 500, 500);
            await client.SendAsync(tamerStatus.Serialize());

            var partnerStatus = new dmoserver.game.Packets.UpdateStatusPacket(
                player.PartnerHandle,
                player.PartnerStats.CurrentHp,
                player.PartnerStats.MaxHp,
                player.PartnerStats.CurrentDs,
                player.PartnerStats.MaxDs
            );
            await client.SendAsync(partnerStatus.Serialize());

            // Confirmar velocidad en mapa (Tamer 600, Partner 600)
            await client.SendAsync(new dmoserver.game.Packets.UpdateMovementSpeedPacket(
                player.TamerHandle, 
                player.PartnerHandle, 
                600, 
                (short)player.PartnerStats.MoveSpeed
            ).Serialize());

            player.LastLoadedMapId = mapId;
        }
    }
}
