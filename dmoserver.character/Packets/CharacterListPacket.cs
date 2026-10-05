using dmoserver.character.Network;

namespace dmoserver.character.Packets;

public class CharacterListPacket : PacketWriter
{
    private const int PacketNumber = 1301;

    // Constructor para lista vacía (permite al cliente entrar a la pantalla y crear personaje)
    public CharacterListPacket()
    {
        Type(PacketNumber);
        
        // 99 indica al cliente el final de la lista de personajes
        WriteByte(99);
    }
}