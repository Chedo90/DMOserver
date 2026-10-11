namespace dmoserver.game.Packets.Entities;

using System.IO;

public class MobHaltPacket
{
    public uint Handle { get; set; }

    public MobHaltPacket(uint handle)
    {
        Handle = handle;
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((short)14);           // Longitud extraída de la captura
        writer.Write((short)1070);         // Opcode 1070[cite: 4]
        writer.Write(Handle);              // Handle del Mob
        writer.Write((int)0);              // Padding
        writer.Write((short)0x1A32);       // Marcador final (32 1A)[cite: 4]

        return ms.ToArray();
    }
}