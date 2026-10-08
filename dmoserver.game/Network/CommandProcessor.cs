namespace dmoserver.game.Network;

using System;
using System.Threading.Tasks;

public static class CommandProcessor
{
    public static async Task<bool> ExecuteAsync(GameClient client, string input)
    {
        // Validamos que exista texto y que empiece por "/"
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return false;

        // Quitamos el "/" inicial y separamos por espacios
        string[] parts = input[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        string command = parts[0].ToLower();
        string[] args = parts[1..];

        switch (command)
        {
            case "coordenadas":
            case "pos":
            {
                // Asumiendo que client.CurrentTamerX y Y siguen existiendo en tu código actual
                await client.SendChatMessageAsync($"Posición actual: X={client.CurrentTamerX}, Y={client.CurrentTamerY}");
                break;
            }

            case "teleport":
            case "tp":
            {
                if (args.Length >= 2 && int.TryParse(args[0], out int x) && int.TryParse(args[1], out int y))
                {
                    // Llama a tu método existente de teletransporte
                    await client.TeleportAsync(x, y);
                    await client.SendChatMessageAsync($"Teletransportado a ({x}, {y})");
                }
                else
                {
                    await client.SendChatMessageAsync("Uso: /teleport <X> <Y>");
                }
                break;
            }

            default:
            {
                await client.SendChatMessageAsync($"Comando no reconocido: /{command}");
                break;
            }
        }

        return true;
    }
}