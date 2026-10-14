namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class UpdateCurrentHPRatePacket
{
    private readonly uint _handle;
    private readonly byte _hpRate;

    public short Opcode => 1007;

    public UpdateCurrentHPRatePacket(uint handle, byte hpRate)
    {
        _handle = handle;
        _hpRate = hpRate;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);
        writer.WriteInt((int)_handle);
        writer.WriteByte(_hpRate);
        return writer.Serialize();
    }
}
