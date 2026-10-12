using dmoserver.game.Network;
using dmoserver.game.Entities; // Necesario para acceder a PlayerEntity

namespace dmoserver.game.Packets;

public class StatUpdatePacket
{
    private readonly PlayerEntity _player;
    public short Opcode => 1033; // 0x0409

    public StatUpdatePacket(PlayerEntity player)
    {
        _player = player;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);
        
        // Usamos el handle dinámico de tu PlayerEntity
        writer.WriteUInt(_player.PartnerHandle);
        
        // Escribimos las estadísticas usando tu propiedad PartnerStats y PartnerLevel
        writer.WriteShort((short)_player.PartnerLevel);
        writer.WriteInt(_player.PartnerStats.MaxHp);
        writer.WriteInt(_player.PartnerStats.MaxDs);
        writer.WriteInt(_player.PartnerStats.Attack);
        writer.WriteInt(_player.PartnerStats.Defense);
        
        writer.WriteShort(0x1A36); // Delimitador final

        return writer.Serialize();
    }
}