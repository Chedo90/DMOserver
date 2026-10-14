using System.Collections.Generic;
using System.Linq;
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

            foreach (var character in characters.OrderBy(c => c.Slot))
            {
                // Defensivo: si el documento en Mongo trae estos campos a null, se usan valores por defecto
                var location = character.Location ?? new CharacterLocation();
                var partner = character.Partner ?? new PartnerDigimonDocument();

                WriteByte(character.Slot);
                WriteShort((short)location.MapId);          // MapId real del personaje
                WriteInt(character.Model);
                WriteByte(ToLevelByte(character.Level));
                WriteString(character.Name);

                // 13 slots de equipamiento vacíos (68 bytes cada uno)
                for (int i = 0; i < EquipmentSlots; i++)
                {
                    WriteBytes(emptyItem);
                }

                WriteInt(partner.Model);
                WriteByte(ToLevelByte(partner.Level));      // Nivel real del Digimon
                WriteString(partner.Name);
                WriteShort(partner.Size > 0 ? partner.Size : (short)10000); // Tamaño / Scale (10000 = 100.00%)

                // 3 shorts de cierre por personaje (flags y seal leader)
                WriteShort(0);
                WriteShort(0);
                WriteShort(0);
            }
        }

        // Byte 99 indica al cliente el fin de la lista
        WriteByte(99);
    }

    private static byte ToLevelByte(short level) => (byte)Math.Clamp((int)level, 1, 255);
}