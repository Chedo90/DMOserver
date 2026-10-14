namespace dmoserver.game.Packets;

using System.IO;

public class MobSpawnPacket
{
    public short Length => 55;
    public short Opcode => 1006;

    private readonly uint _handle;
    private readonly int _type;
    private readonly int _x;
    private readonly int _y;
    private readonly byte _level;

    public MobSpawnPacket(uint handle, int type, int x, int y, byte level = 5)
    {
        _handle = handle;
        _type = type;
        _x = x;
        _y = y;
        _level = level;
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(Length);             // 2 bytes
        writer.Write(Opcode);             // 2 bytes
        writer.Write((byte)1);            // 1 byte: Action
        writer.Write((short)1);           // 2 bytes: Sub-action

        writer.Write(_x);                 // 4 bytes
        writer.Write(_y);                 // 4 bytes
        writer.Write(_handle);            // 4 bytes
        writer.Write((uint)_type);        // 4 bytes
        writer.Write(_x);                 // 4 bytes
        writer.Write(_y);                 // 4 bytes

        // Bloque de Estadísticas (Alineado correctamente)
        writer.Write((byte)100);          // 1 byte: CurrentHpRate (%)
        writer.Write((short)_level);      // 2 bytes: ¡Nivel real desde BD!
        writer.Write((short)2);           // 2 bytes: Skill Idx
        writer.Write((int)0);             // 4 bytes: GrowStack
        writer.Write((int)0);             // 4 bytes: SyncCondition
        writer.Write((int)0);             // 4 bytes: DisposedObjects
        writer.Write((byte)0);            // 1 byte: Padding

        // Cierre (Para sumar exactamente 55 bytes)
        writer.Write((int)0);             // 4 bytes
        writer.Write((short)0x1A0B);      // 2 bytes: Delimitador GDMO

        return ms.ToArray();
    }
}