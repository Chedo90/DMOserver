using System.Text;
using dmoserver.character.Enums;

namespace dmoserver.character.Network;

public class CharacterPacketReader
{
    private readonly byte[] _buffer;
    private int _position;

    public short Length { get; }
    public short Type { get; }
    public CharacterServerPacketEnum Enum => (CharacterServerPacketEnum)Type;

    public CharacterPacketReader(byte[] data)
    {
        _buffer = data;
        _position = 0;

        if (data.Length >= 4)
        {
            Length = ReadShort();
            Type = ReadShort();
        }
    }

    public void Seek(int offset) => _position = offset;
    public void Skip(int count) => _position += count;

    public byte ReadByte() => _buffer[_position++];
    public short ReadShort()
    {
        short val = BitConverter.ToInt16(_buffer, _position);
        _position += 2;
        return val;
    }
    public int ReadInt()
    {
        int val = BitConverter.ToInt32(_buffer, _position);
        _position += 4;
        return val;
    }
    public uint ReadUInt()
    {
        uint val = BitConverter.ToUInt32(_buffer, _position);
        _position += 4;
        return val;
    }

    // Lee texto dinámico con longitud
    public string ReadString()
    {
        byte len = ReadByte();
        string val = Encoding.ASCII.GetString(_buffer, _position, len);
        _position += len;
        return val;
    }

    // Lee cadena terminada en cero (Null-Terminated)
    public string ReadZString()
    {
        int start = _position;
        while (_position < _buffer.Length && _buffer[_position] != 0)
        {
            _position++;
        }
        string val = Encoding.ASCII.GetString(_buffer, start, _position - start);
        if (_position < _buffer.Length) _position++; // salta el byte 0
        return val;
    }
}