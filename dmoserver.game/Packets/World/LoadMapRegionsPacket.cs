namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class LoadMapRegionsPacket
{
    private readonly uint _handle;
    private readonly int _modelId;
    private readonly string _name;
    private readonly int _x;
    private readonly int _y;

    public LoadMapRegionsPacket(uint handle, int modelId, string name, int x, int y)
    {
        _handle = handle;
        _modelId = modelId;
        _name = name;
        _x = x;
        _y = y;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(16041); // Opcode 0x3EA9

        // 1. Cantidad total de Mobs en la lista (1 mob para prueba)
        writer.WriteShort(1); 

        // 2. Handle / Id de la entidad (4 bytes)
        writer.WriteUInt(_handle);

        // 3. Model ID (4 bytes) - 45141 (Devidramon)
        writer.WriteInt(_modelId);

        // 4. Longitud del nombre (1 byte)
        writer.WriteByte((byte)_name.Length);

        // 5. Nombre en ASCII + byte nulo (11 bytes en total para Devidramon)
        writer.WriteString(_name);

        // 6. Subtype / Flag (4 bytes) -> En el dump oficial: 00 01 00 00
        writer.WriteByte(0x00);
        writer.WriteByte(0x01);
        writer.WriteByte(0x00);
        writer.WriteByte(0x00);

        // 7. Coordenada X (4 bytes)
        writer.WriteInt(_x);

        // 8. Coordenada Y (4 bytes)
        writer.WriteInt(_y);

        // 9. Extra / Padding (4 bytes) -> En el dump: 00 00 00 10
        writer.WriteInt(16);

        return writer.Serialize();
    }
}