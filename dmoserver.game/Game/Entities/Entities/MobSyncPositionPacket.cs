namespace dmoserver.game.Packets.Entities;

using System.IO;

public class MobSyncPositionPacket
{
    public uint Handle { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public MobSyncPositionPacket(uint handle, int x, int y)
    {
        Handle = handle;
        X = x;
        Y = y;
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((short)25);           // Longitud exacta
        writer.Write((short)1006);         // Opcode 1006 (Movimiento/Sync de Mobs)
        writer.Write((byte)5);             // Flag de acción (5 = Caminar hacia destino)
        writer.Write((short)1);            // Sub-estado
        writer.Write(Handle);              // 0x10000 | idx
        writer.Write(X);                   // Destino X
        writer.Write(Y);                   // Destino Y
        writer.Write((int)0);              // Yaw
        writer.Write((short)0x1A25);       // Marcador final

        return ms.ToArray();
    }
}