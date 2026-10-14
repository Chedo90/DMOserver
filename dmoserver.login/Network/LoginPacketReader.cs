namespace dmoserver.login.Network;

using System.Text;

public sealed class LoginPacketReader
{
    private readonly byte[] _data;
    private readonly int _limit; // fin real del paquete (nunca se lee más allá)
    private int _offset;

    public short Length { get; }
    public short Type { get; }

    public LoginPacketReader(byte[] data, int length)
    {
        if (data.Length < 4)
            throw new InvalidDataException("Paquete demasiado corto.");

        _data = data;
        _limit = Math.Min(length, data.Length);
        Length = BitConverter.ToInt16(data, 0);
        Type = BitConverter.ToInt16(data, 2);
        _offset = 4;
    }

    public void Skip(int count) => _offset += count;

    public byte ReadByte()
    {
        Ensure(1);
        return _data[_offset++];
    }

    public short ReadShort()
    {
        Ensure(2);
        short value = BitConverter.ToInt16(_data, _offset);
        _offset += 2;
        return value;
    }

    public int ReadInt()
    {
        Ensure(4);
        int value = BitConverter.ToInt32(_data, _offset);
        _offset += 4;
        return value;
    }

    public uint ReadUInt()
    {
        Ensure(4);
        uint value = BitConverter.ToUInt32(_data, _offset);
        _offset += 4;
        return value;
    }

    /// <summary>
    /// Lee una cadena con formato [longitud][texto][0].
    /// Consume el byte nulo terminador si está presente (mismo comportamiento que el Character Server).
    /// Devuelve cadena vacía si el paquete está malformado.
    /// </summary>
    public string ReadString()
    {
        if (_offset >= _limit) return string.Empty;

        byte length = _data[_offset++];

        if (_offset + length > _limit)
        {
            _offset = _limit;
            return string.Empty;
        }

        string text = Encoding.ASCII.GetString(_data, _offset, length);
        _offset += length;

        if (_offset < _limit && _data[_offset] == 0)
            _offset++;

        return text.TrimEnd('\0');
    }

    private void Ensure(int count)
    {
        if (_offset + count > _limit)
            throw new InvalidDataException("Lectura fuera de los límites del paquete.");
    }
}