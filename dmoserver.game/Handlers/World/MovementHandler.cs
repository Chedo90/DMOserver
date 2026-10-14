namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class MovementHandler : IPacketHandler
{
    public Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        // Formato del paquete 1004: [ticks:int][handler:int][x:int][y:int][z:float]
        packet.ReadInt();                 // ticks (no se usa)
        int handle = packet.ReadInt();    // quién se mueve: Tamer (>= short.MaxValue) o Partner
        int destX = packet.ReadInt();
        int destY = packet.ReadInt();
        float destZ = BitConverter.Int32BitsToSingle(packet.ReadInt());

        var player = client.Player;
        if (player == null) return Task.CompletedTask;

        // Igual que Oldserver: handler >= short.MaxValue => Tamer, si no => Partner
        bool isPartner = handle < short.MaxValue;

        if (isPartner)
        {
            player.PartnerX = destX;
            player.PartnerY = destY;
            player.PartnerZ = destZ;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[📍 POSICIÓN PARTNER (0x{handle:X})] X: {destX} | Y: {destY} | Z: {destZ}");
            Console.ResetColor();
        }
        else
        {
            player.X = destX;
            player.Y = destY;
            player.Z = destZ;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[📍 POSICIÓN TAMER (0x{handle:X})] X: {destX} | Y: {destY} | Z: {destZ}");
            Console.ResetColor();

            // Guardar como máximo cada 5 segundos la posición del Tamer en BD
            var now = DateTime.UtcNow;
            if (player.Account != null && player.Character != null &&
                (now - client.LastPositionSave).TotalSeconds >= 5)
            {
                client.LastPositionSave = now;
                _ = GameClient.Db.UpdatePositionAsync(player.Account.AccountId, player.Character.Slot, destX, destY, destZ);
            }
        }

        return Task.CompletedTask;
    }
}
