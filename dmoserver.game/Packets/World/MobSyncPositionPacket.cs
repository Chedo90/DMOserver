namespace dmoserver.game.Packets.Entities;

using dmoserver.game.Network;

public class MobSyncPositionPacket
{
    public uint Handle { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public byte Action { get; set; }

    public MobSyncPositionPacket(uint handle, int x, int y, byte action = 5)
    {
        Handle = handle;
        X = x;
        Y = y;
        Action = action;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1006);
        writer.WriteByte(Action);             // 5 = Caminar, 6 = Correr
        writer.WriteShort(1);                 // Sub-estado
        writer.WriteUInt(Handle);
        writer.WriteInt(X);
        writer.WriteInt(Y);
        writer.WriteInt(0);                   // Yaw
        return writer.Serialize();
    }
}