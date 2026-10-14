namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

public class MobTargetPacket
{
    private readonly uint _handle;
    private readonly byte _level;
    private readonly byte _hpRate;
    private readonly uint _tamerHandle;
    private readonly int _startTime;
    private readonly int _endTime;

    public MobTargetPacket(uint handle, byte level, byte hpRate, uint tamerHandle = 0, int startTime = 0, int endTime = 0)
    {
        _handle = handle;
        _level = level;
        _hpRate = hpRate;
        _tamerHandle = tamerHandle;
        _startTime = startTime;
        _endTime = endTime;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1017);
        writer.WriteUInt(_handle);
        writer.WriteByte(_hpRate);
        writer.WriteUInt(_tamerHandle);
        writer.WriteInt(_endTime);
        writer.WriteInt(_startTime);
        return writer.Serialize();
    }
}
