namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class MobSpawnPacket
{
    private readonly uint _handle;
    private readonly int _type;
    private readonly int _x;
    private readonly int _y;
    private readonly byte _action;

    public MobSpawnPacket(uint handle, int type, int x, int y, byte action = 1)
    {
        _handle = handle;
        _type = type;
        _x = x;
        _y = y;
        _action = action;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1006);

        writer.WriteByte(_action);
        writer.WriteShort(1);

        // Coordenadas anteriores
        writer.WriteInt(_x);
        writer.WriteInt(_y);

        // Handle
        writer.WriteInt((int)_handle);

        // Type de especie
        writer.WriteInt(_type);

        // Coordenadas actuales
        writer.WriteInt(_x);
        writer.WriteInt(_y);

        // Stats y estados
        writer.WriteByte(100); // CurrentHpRate
        writer.WriteShort(1);  // Level
        writer.WriteShort(2);  // Skill Idx
        writer.WriteInt(0);    // GrowStack
        writer.WriteInt(0);    // SyncCondition
        writer.WriteInt(0);    // DisposedObjects
        writer.WriteByte(0);   // Padding final

        writer.WriteInt(0);    // Cierre

        return writer.Serialize();
    }
}