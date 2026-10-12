namespace dmoserver.game.Packets.Digimon;

using System.IO;
using System.Threading.Tasks;

public class SkillLevelUpHandler
{
    public const short Opcode = 1104;

    public static async Task HandleAsync(byte[] packetData)
    {
        using var stream = new MemoryStream(packetData);
        using var reader = new BinaryReader(stream);

        // Si tu array packetData incluye el opcode o tamaño al principio, 
        // puedes omitirlos con reader.ReadBytes(offset) según tu arquitectura.
        int generalHandler = reader.ReadInt32();
        byte formSlot = reader.ReadByte();   // 1 = Rookie, 2 = Champion, etc.
        byte skillSlot = reader.ReadByte();  // 0 = F1, 1 = F2, etc.

        await Task.CompletedTask;
    }
}