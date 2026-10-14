namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public sealed class UpdateMovementSpeedPacket(uint tamerHandle, uint partnerHandle, short tamerSpeed = 600, short partnerSpeed = 600)
{
    private const int PacketNumber = 9905;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteUInt(tamerHandle);
        writer.WriteUInt(partnerHandle);

        writer.WriteShort(tamerSpeed);
        writer.WriteShort(partnerSpeed);

        writer.WriteInt(0);
        writer.WriteInt(0);

        return writer.Serialize();
    }
}