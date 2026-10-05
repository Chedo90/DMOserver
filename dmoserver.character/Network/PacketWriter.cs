using System.Text;

namespace dmoserver.character.Network;

public class PacketWriter
{
    private const short CheckSumValidation = 6716;
    private readonly List<byte> _body = new();
    protected short PacketType;

    public PacketWriter() { }

    public PacketWriter(short type)
    {
        PacketType = type;
    }

    public void Type(int packetNumber) => PacketType = (short)packetNumber;

    public void WriteByte(byte value) => _body.Add(value);
    public void WriteBytes(byte[] buffer) => _body.AddRange(buffer);
    public void WriteShort(short value) => _body.AddRange(BitConverter.GetBytes(value));
    public void WriteInt(int value) => _body.AddRange(BitConverter.GetBytes(value));
    public void WriteUInt(uint value) => _body.AddRange(BitConverter.GetBytes(value));

    public void WriteString(string value)
    {
        byte[] textBytes = Encoding.ASCII.GetBytes(value);
        _body.Add((byte)textBytes.Length);
        _body.AddRange(textBytes);
        _body.Add(0);
    }

    public byte[] Serialize()
    {
        short totalLength = (short)(_body.Count + 6);
        short checksum = (short)(totalLength ^ CheckSumValidation);

        List<byte> packet = new List<byte>(totalLength);
        packet.AddRange(BitConverter.GetBytes(totalLength));
        packet.AddRange(BitConverter.GetBytes(PacketType));
        packet.AddRange(_body);
        packet.AddRange(BitConverter.GetBytes(checksum));

        return packet.ToArray();
    }
}