namespace dmoserver.game.Handlers;

using System;
using dmoserver.game.Network;

public static class TargetHandler
{
    private const uint ClientPartnerHandle = 0x4000;

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
        else if (client.CurrentMap?.GetMonster(targetHandle) != null)
            Console.WriteLine($"[Target] Mob seleccionado: 0x{targetHandle:X}");
        else
            Console.WriteLine($"[Target] Entidad desconocida: 0x{targetHandle:X}");
        Console.ResetColor();
    }
}