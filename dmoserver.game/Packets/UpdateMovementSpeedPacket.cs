namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public sealed class UpdateMovementSpeedPacket(uint tamerHandle = 100000, uint partnerHandle = 100001, short speed = 600)
{
    private const int PacketNumber = 9905;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteUInt(tamerHandle);
        writer.WriteUInt(partnerHandle);

        // Velocidad de movimiento (Tamer y Partner)
        writer.WriteShort(speed);
        writer.WriteShort(speed);

        // Condición normal (0)
        writer.WriteInt(0);
        writer.WriteInt(0);

        return writer.Serialize();
    }
}