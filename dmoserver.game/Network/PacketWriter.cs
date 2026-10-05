namespace dmoserver.game.Network;

using System.Buffers.Binary;
using System.Text;

public sealed class PacketWriter : IDisposable
{
    public const short CheckSumValidation = 6716;

    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public PacketWriter(int type)
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);

        // Cabecera DMO: [Length (2B)] + [Type (2B)]
        _writer.Write((short)0);
        _writer.Write((short)type);
    }

    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteShort(short value) => _writer.Write(value);
    public void WriteUShort(ushort value) => _writer.Write(value);
    public void WriteInt(int value) => _writer.Write(value);
    public void WriteUInt(uint value) => _writer.Write(value);
    public void WriteLong(long value) => _writer.Write(value);
    public void WriteFloat(float value) => _writer.Write(value);

    public void WriteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _writer.Write((byte)0);
            _writer.Write((byte)0);
            return;
        }

        byte[] bytes = Encoding.ASCII.GetBytes(value);
        _writer.Write((byte)bytes.Length); // 1 byte de longitud
        _writer.Write(bytes);              // Contenido ASCII
        _writer.Write((byte)0);            // Null terminator
    }

    public byte[] Serialize()
    {
        _writer.Write((short)0); // Reserva para Checksum
        _writer.Flush();

        byte[] buffer = _stream.ToArray();
        short length = (short)buffer.Length;
        short checksum = (short)(length ^ CheckSumValidation);

        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(0, 2), length);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(buffer.Length - 2, 2), checksum);

        return buffer;
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}