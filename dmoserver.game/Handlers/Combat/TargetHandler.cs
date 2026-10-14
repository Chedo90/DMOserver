namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class TargetHandler : IPacketHandler
{
    private const uint ClientPartnerHandle = 0x4000;

    public Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        Console.WriteLine($"[1016 RAW] {packet.ToHexString()}");
        HandleTargetSelection(client, packet);
        return Task.CompletedTask;
    }

    public static void HandleTargetSelection(GameClient client, GamePacketReader packet)
    {
        uint selectorHandle = packet.ReadUInt(); // quién selecciona (Tamer 0x8000)
        uint targetHandle = packet.ReadUInt();   // objetivo real

        client.Player.TargetHandle = targetHandle;

        Console.ForegroundColor = ConsoleColor.Cyan;
        if (targetHandle == 0)
            Console.WriteLine("[Target] Objetivo deseleccionado.");
        else if (targetHandle == selectorHandle || targetHandle == client.Player.TamerHandle)
            Console.WriteLine($"[Target] Tamer seleccionado (0x{targetHandle:X})");
        else if (targetHandle == client.Player.PartnerHandle || targetHandle == ClientPartnerHandle)
            Console.WriteLine($"[Target] Digimon partner seleccionado (0x{targetHandle:X})");
        else
        {
            var mob = client.CurrentMap?.GetMonster(targetHandle);
            if (mob != null)
            {
                byte hpRate = (byte)Math.Clamp((mob.CurrentHp * 100) / Math.Max(1, mob.Stats.MaxHp), 0, 100);
                Console.WriteLine($"[Target] Mob seleccionado: 0x{targetHandle:X} (HP: {mob.CurrentHp}/{mob.Stats.MaxHp} [{hpRate}%], LV: {mob.Stats.Level})");

                var targetPacket = new dmoserver.game.Packets.Combat.MobTargetPacket(
                    mob.Handle, 
                    (byte)mob.Stats.Level, 
                    hpRate, 
                    0, 
                    0, 
                    0
                ).Serialize();
                _ = client.SendAsync(targetPacket);
                _ = client.SendAsync(new dmoserver.game.Packets.Combat.UpdateCurrentHPRatePacket(mob.Handle, hpRate).Serialize());
            }
            else
            {
                Console.WriteLine($"[Target] Entidad desconocida: 0x{targetHandle:X}");
            }
        }
        Console.ResetColor();
    }
}