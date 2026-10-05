namespace dmoserver.login.Network;

using System.Text;

public sealed class LoginPacketReader
{
    private readonly byte[] _data;
    private int _offset;

    public short Length { get; }
    public short Type { get; }

    public LoginPacketReader(byte[] data, int length)
    {
        _data = data;
        Length = BitConverter.ToInt16(data, 0);
        Type = BitConverter.ToInt16(data, 2);
        _offset = 4;
    }

    public void Skip(int count) => _offset += count;
    public byte ReadByte() => _data[_offset++];
    public short ReadShort() => BitConverter.ToInt16(_data, (_offset += 2) - 2);
    public int ReadInt() => BitConverter.ToInt32(_data, (_offset += 4) - 4);
    public uint ReadUInt() => BitConverter.ToUInt32(_data, (_offset += 4) - 4);

    public string ReadString()
    {
        if (_offset >= _data.Length) return string.Empty;
        byte length = ReadByte();
        if (length == 0 || _offset + length > _data.Length) return string.Empty;

        string str = Encoding.ASCII.GetString(_data, _offset, length);
        _offset += length + 1; // saltamos el texto y el byte nulo terminador
        return str.TrimEnd('\0');
    }
}