namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;
using dmoserver.game.Packets;

public sealed class PortalHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        int portalId = packet.ReadInt();
        short channel = packet.ReadShort();

        // 1. Buscar la información del portal en la colección MongoDB 'portals'
        var portal = await GameClient.Db.GetPortalAsync(portalId);

        if (portal == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Portal] AVISO: El cliente solicitó el Portal ID {portalId}, pero no existe en MongoDB.");
            Console.ResetColor();

            await client.SendChatMessageAsync($"[Servidor] El portal ID {portalId} no está registrado en la base de datos.");
            return;
        }

        // 2. Comprobar requerimientos (Nivel de Tamer)
        if (portal.RequiredTamerLevel > 0 && client.Player.TamerLevel < portal.RequiredTamerLevel)
        {
            await client.SendChatMessageAsync($"[Servidor] Nivel insuficiente. Requieres nivel {portal.RequiredTamerLevel} de Tamer para viajar.");
            return;
        }

        // 3. Comprobar requerimientos de Bits (coste de viaje fijado en NPC.xml o en MongoDB)
        if (portal.CostBits > 0)
        {
            if (client.Player.Bits < portal.CostBits)
            {
                await client.SendChatMessageAsync($"[Servidor] No tienes suficientes Bits. El coste del viaje es de {portal.CostBits:N0} Bits.");
                return;
            }

            // Deducir Bits y guardar en MongoDB
            client.Player.Bits -= portal.CostBits;
            if (client.Player.Account != null && client.Player.Character != null)
            {
                await GameClient.Db.UpdateBitsAsync(client.Player.Account.AccountId, client.Player.Character.Slot, client.Player.Bits);
            }

            await client.SendChatMessageAsync($"[Servidor] Pagados {portal.CostBits:N0} Bits por el viaje. Saldo actual: {client.Player.Bits:N0} Bits.");
        }

        int currentMapId = client.Player.Character?.Location.MapId ?? client.CurrentMap?.MapId ?? 0;
        bool isLocal = portal.DestMapId == currentMapId || portal.SrcMapId == portal.DestMapId;

        // 4. Caso A: Teletransporte dentro del mismo mapa (LocalMapSwap - Opcode 1711)
        if (isLocal)
        {
            client.Player.X = portal.DestX;
            client.Player.Y = portal.DestY;
            client.Player.PartnerX = portal.DestX;
            client.Player.PartnerY = portal.DestY;

            if (client.Player.Account != null && client.Player.Character != null)
            {
                client.Player.Character.Location.X = portal.DestX;
                client.Player.Character.Location.Y = portal.DestY;
                await GameClient.Db.UpdatePositionAsync(
                    client.Player.Account.AccountId,
                    client.Player.Character.Slot,
                    portal.DestX,
                    portal.DestY,
                    client.Player.Z
                );
            }

            var localPacket = new LocalMapSwapPacket(
                (int)client.Player.TamerHandle,
                (int)client.Player.PartnerHandle,
                portal.DestX,
                portal.DestY,
                portal.DestX,
                portal.DestY
            );

            await client.SendAsync(localPacket.Serialize());

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Portal] Teletransporte LOCAL en Mapa {currentMapId} -> ({portal.DestX}, {portal.DestY}) para '{client.Player.Character?.Name}'.");
            Console.ResetColor();
            return;
        }

        // 5. Caso B: Teletransporte a otro mapa (MapSwapPacket - Opcode 1709)
        // Desvincular de la instancia del mapa origen
        client.CurrentMap?.Players.TryRemove(client.EndPoint, out _);
        client.SetCurrentMap(null!);

        // Actualizar coordenadas y mapa en memoria
        if (client.Player.Character != null)
        {
            client.Player.Character.Location.MapId = portal.DestMapId;
            client.Player.Character.Location.X = portal.DestX;
            client.Player.Character.Location.Y = portal.DestY;
        }

        client.Player.X = portal.DestX;
        client.Player.Y = portal.DestY;
        client.Player.PartnerX = portal.DestX;
        client.Player.PartnerY = portal.DestY;

        // Resetear LastLoadedMapId para que LoadingFinishedHandler (Opcode 1001) configure el nuevo mapa
        client.Player.LastLoadedMapId = -1;

        // Guardar la nueva ubicación en MongoDB
        if (client.Player.Account != null && client.Player.Character != null)
        {
            await GameClient.Db.UpdateLocationAsync(
                client.Player.Account.AccountId,
                client.Player.Character.Slot,
                portal.DestMapId,
                portal.DestX,
                portal.DestY,
                client.Player.Z
            );
        }

        // Enviar paquete de cambio de mapa al cliente para abrir la pantalla de carga
        await client.SendAsync(new MapSwapPacket("127.0.0.1", 7031, portal.DestMapId, portal.DestX, portal.DestY).Serialize());

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Portal] Teletransporte EXTERNO: '{client.Player.Character?.Name}' viaja al Mapa {portal.DestMapId} ({portal.DestX}, {portal.DestY}) [Portal ID: {portal.PortalId}].");
        Console.ResetColor();
    }
}
