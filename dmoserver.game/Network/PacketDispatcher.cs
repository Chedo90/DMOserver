namespace dmoserver.game.Network;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using dmoserver.game.Handlers;

public static class PacketDispatcher
{
    private static readonly FrozenDictionary<short, IPacketHandler> _handlers;

    static PacketDispatcher()
    {
        var attackHandler = new AttackHandler();
        var silentIgnoreHandler = new IgnorePacketHandler();

        var handlers = new Dictionary<short, IPacketHandler>
        {
            [-1] = new HandshakeHandler(),
            [-3] = silentIgnoreHandler,
            [1001] = new LoadingFinishedHandler(),
            [1004] = new MovementHandler(),
            [1008] = new ChatHandler(),
            [1013] = attackHandler,
            [1015] = new SkillAttackHandler(),
            [1016] = new TargetHandler(),
            [1022] = new DieConfirmHandler(),
            [1033] = new RawDumpHandler(),
            [1104] = new SkillLevelUpHandler(),
            [1302] = new CharacterSelectHandler(),
            [1703] = new ConnectGameServerHandler(),
            [1706] = new InitialInformationHandler(),
            [1709] = new PortalHandler(),
            [1713] = new IgnorePacketHandler("[1713] recibido (ignorado)")
        };

        _handlers = handlers.ToFrozenDictionary();
    }

    public static async Task DispatchAsync(GameClient client, GamePacketReader packet)
    {
        if (_handlers.TryGetValue(packet.Type, out var handler))
        {
            await handler.HandleAsync(client, packet);
        }
        else if (packet.Type != 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[!] Opcode no manejado: {packet.Type} (Tamaño: {packet.Length})");
            Console.ResetColor();
        }
    }
}
