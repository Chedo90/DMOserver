using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class CharacterCreatedPacket : PacketWriter
{
    private const int PacketNumber = 1306;
    private const int EquipmentSlots = 13; // Equipment = 13 en GeneralSizeEnum

    public CharacterCreatedPacket(
        byte position,
        int tamerModel,
        string tamerName,
        int digimonModel,
        string digimonName,
        short handshake,
        short mapId = 105)
    {
        Type(PacketNumber);

        // Bloque inicial de Handshake y padding (16 bytes)
        WriteShort(handshake);
        WriteShort(0);
        WriteShort(0);
        WriteShort(0);
        WriteShort(handshake);
        WriteShort(0);
        WriteShort(0);
        WriteShort(0);

        // Tamer
        WriteByte(position);
        WriteShort(mapId);
        WriteInt(tamerModel);
        WriteByte(1); // Nivel 1 inicial
        WriteString(tamerName);

        // 13 slots de equipamiento vacíos (13 * 68 = 884 bytes)
        byte[] emptyItemSlot = new byte[68];
        for (int i = 0; i < EquipmentSlots; i++)
        {
            WriteBytes(emptyItemSlot);
        }

        // Digimon inicial
        WriteInt(digimonModel);
        WriteByte(1); // Nivel 1 inicial
        WriteString(digimonName);

        // 8 bytes de cierre en cero
        for (int i = 0; i < 8; i++)
        {
            WriteByte(0);
        }
    }
}