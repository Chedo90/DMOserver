using System.Text;
using dmoserver.login.Network;

namespace dmoserver.login.Packet;

public static class ConnectCharacterServerPacket
{
    public static byte[] Create(long accountId, string ipAddress, int port)
    {
        var writer = new PacketWriter(901);

        // Offset 4: AccountId (uint, 4 bytes)
        writer.WriteUInt((uint)accountId);

        // Offset 8: AccountId (int, 4 bytes)
        writer.WriteInt((int)accountId);

        // Offset 12: WriteString real (1 byte longitud + texto ASCII + 1 byte nulo)
        byte[] ipBytes = Encoding.ASCII.GetBytes(ipAddress);
        writer.WriteByte((byte)ipBytes.Length); // Byte con valor 9
        foreach (byte b in ipBytes)
        {
            writer.WriteByte(b);               // "127.0.0.1"
        }
        writer.WriteByte(0);                   // Byte terminador 0x00

        // Inmediatamente después del texto: Puerto (uint, 4 bytes)
        writer.WriteUInt((uint)port);

        return writer.Build();
    }
}