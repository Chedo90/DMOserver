using System.Net.Sockets;
using dmoserver.character.Network;

namespace dmoserver.character;

public class CharacterPacketProcessor
{
    public static async Task ProcessAsync(short opcode, byte[] payload, NetworkStream stream)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[<-] Character Server procesando Opcode: {opcode} (Payload: {payload.Length} bytes)");
        Console.ResetColor();

        switch (opcode)
        {
            // Aquí irán los opcodes de carga de personajes que el cliente envía al conectar al puerto 7030.
            // Por ejemplo, el paquete de conexión inicial del tamer.
            default:
                Console.WriteLine($"[!] Opcode de Character Server no mapeado: {opcode}");
                break;
        }
    }
}