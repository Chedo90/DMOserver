using dmoserver.character.Network;
using dmoserver.database;

namespace dmoserver.character.Packets;

public class CharacterListPacket : PacketWriter
{
    private const int PacketNumber = 1301;
    private const int EquipmentSlots = 13;

    public CharacterListPacket(IEnumerable<CharacterDocument>? characters = null)
    {
        Type(PacketNumber);

        if (characters != null)
        {
            byte[] emptyItem = new byte[68];

            foreach (var character in characters.Where(c => c.Partner != null))
            {
                WriteByte(character.Slot);
                WriteShort(105); // MapId por defecto (D-Terminal / DATS)
                WriteInt(character.Model);
                WriteByte((byte)(character.Level > 0 ? character.Level : 1));
                WriteString(character.Name);

                // 13 slots de equipamiento vacíos (68 bytes cada uno)
                for (int i = 0; i < EquipmentSlots; i++)
                {
                    WriteBytes(emptyItem);
                }

                WriteInt(character.Partner.Model);
                WriteByte(1); // Nivel 1 inicial del Digimon
                WriteString(character.Partner.Name);
                WriteShort(10000); // Tamaño / Scale (100.00%)

                // 3 shorts de cierre por personaje (flags y seal leader)
                WriteShort(0);
                WriteShort(0);
                WriteShort(0);
            }
        }

        // Byte 99 indica al cliente el fin de la lista
        WriteByte(99);
    }
}