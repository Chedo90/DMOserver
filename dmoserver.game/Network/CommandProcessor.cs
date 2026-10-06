namespace dmoserver.game.Network;

using System;
using System.Threading.Tasks;

public static class CommandProcessor
{
    public static async Task<bool> ExecuteAsync(GameClient client, string input)
    {
        if (string.IsNullOrWhiteSpace(input) || (!input.StartsWith('!') && !input.StartsWith('/')))
            return false;

        string[] parts = input[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        string command = parts[0].ToLower();
        string[] args = parts[1..];

        switch (command)
        {
            case "help":
            case "comandos":
            {
                await client.SendChatMessageAsync("--- COMANDOS DISPONIBLES ---");
                await client.SendChatMessageAsync("!pos - Muestra coordenadas actuales");
                await client.SendChatMessageAsync("!tp <X> <Y> - Teletransporte");
                await client.SendChatMessageAsync("!speed <valor> - Cambia la velocidad");
                break;
            }

            case "pos":
            {
                await client.SendChatMessageAsync($"Posición actual: X={client.CurrentTamerX}, Y={client.CurrentTamerY}");
                break;
            }

            case "tp":
            {
                if (args.Length >= 2 && int.TryParse(args[0], out int x) && int.TryParse(args[1], out int y))
                {
                    await client.TeleportAsync(x, y);
                    await client.SendChatMessageAsync($"Teletransportado a ({x}, {y})");
                }
                else
                {
                    await client.SendChatMessageAsync("Uso: !tp <X> <Y>");
                }
                break;
            }

            case "speed":
            {
                if (args.Length >= 1 && short.TryParse(args[0], out short speed))
                {
                    await client.SetSpeedAsync(speed);
                    await client.SendChatMessageAsync($"Velocidad establecida a: {speed}");
                }
                else
                {
                    await client.SendChatMessageAsync("Uso: !speed <valor>");
                }
                break;
            }

            default:
            {
                await client.SendChatMessageAsync($"Comando no reconocido: !{command}");
                break;
            }
        }

        return true;
    }
}