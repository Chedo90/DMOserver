namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public sealed class InitialInformationHandler : IPacketHandler
{
    public async Task HandleAsync(GameClient client, GamePacketReader packet)
    {
        // 1. Extraemos el AccountId que el cliente envía al saltar al Game Server
        packet.Seek(8);
        uint incomingAccountId = packet.ReadUInt();

        if (incomingAccountId > 0)
        {
            // Asignamos el AccountId real a la sesión actual en memoria
            client.Player.AccountId = incomingAccountId;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] [GameServer] Cliente autorizado entrando al mundo con AccountId: {incomingAccountId}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[!] [GameServer] Advertencia: El AccountId recibido es 0.");
            Console.ResetColor();
        }

        // 2. Ahora delegamos al handler. Como Player.AccountId ya tiene el ID correcto, cargará sus propios personajes.
        await LoginWorldHandler.HandleInitialInformationAsync(client, packet);
    }
}
