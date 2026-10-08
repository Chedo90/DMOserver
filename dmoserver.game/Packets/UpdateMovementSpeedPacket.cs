namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public sealed class UpdateMovementSpeedPacket(uint tamerHandle, uint partnerHandle, short speed = 600)
{
    private const int PacketNumber = 9905;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteUInt(tamerHandle);
        writer.WriteUInt(partnerHandle);

        writer.WriteShort(speed);
        writer.WriteShort(speed);

        writer.WriteInt(0);
        writer.WriteInt(0);

        return writer.Serialize();
    }
}