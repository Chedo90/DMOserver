namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public enum ChatType : byte
{
    Normal = 7,
    Whisper = 8,
    Notice = 9,
    Area = 10,
    Shout = 11,
    Megaphone = 12,
    Guild = 129
}

public sealed class ChatMessagePacket
{
    private readonly byte[] _payload;
    private const int PacketNumber = 1006;

    // CONSTRUCTOR 1: Para Shout y Notice (Avisos del sistema). Usa tu Nombre (String).
    public ChatMessagePacket(string message, string senderName, ChatType type)
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteByte((byte)type);
        writer.WriteByte(1);

        writer.WriteString(senderName);
        writer.WriteString(message);
        writer.WriteByte(0);

        _payload = writer.Serialize();
    }

    // CONSTRUCTOR 2: Para Chat Normal. Usa tu Handle 3D (UInt) para el bocadillo sobre la cabeza.
    public ChatMessagePacket(string message, uint senderHandle, ChatType type = ChatType.Normal)
    {
        using var writer = new PacketWriter(PacketNumber);
        writer.WriteByte((byte)type);
        writer.WriteByte(1);

        writer.WriteUInt(senderHandle);
        writer.WriteString(message);
        writer.WriteByte(0);

        _payload = writer.Serialize();
    }

    public byte[] Serialize() => _payload;
}