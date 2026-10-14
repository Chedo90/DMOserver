namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public enum ChatType : byte
{
    Normal = 11,
    Shout = 9,
    Whisper = 8,
    Notice = 7,
    Megaphone = 12
}

public sealed class ChatMessagePacket
{
    private const int PacketNumber = 1006;
    private readonly byte[] _data;

    public ChatMessagePacket(uint sourceHandler, string message, ChatType type = ChatType.Normal)
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteByte((byte)type);
        writer.WriteByte(1); // Flag estándar
        writer.WriteUInt(sourceHandler);
        writer.WriteString(message); 
        writer.WriteByte(0); // Byte nulo final

        _data = writer.Serialize();
    }

    public byte[] Serialize() => _data;
}