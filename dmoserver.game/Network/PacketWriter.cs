namespace dmoserver.game.Network;

using System;
using System.Buffers.Binary;
using System.IO;
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
    public void WriteBytes(byte[] buffer) => _writer.Write(buffer);
    public void WriteShort(short value) => _writer.Write(value);
    public void WriteUShort(ushort value) => _writer.Write(value);
    public void WriteInt(int value) => _writer.Write(value);
    public void WriteUInt(uint value) => _writer.Write(value);
    public void WriteLong(long value) => _writer.Write(value);
    public void WriteFloat(float value) => _writer.Write(value);

    public void WriteString(string value)
    {
        value ??= string.Empty;
        
        // 1. Convertir a ASCII
        byte[] buffer = Encoding.ASCII.GetBytes(value);
        
        // 2. Escribir la longitud del texto (1 byte)
        WriteByte((byte)buffer.Length);
        
        // 3. Escribir los bytes del texto
        WriteBytes(buffer); 
        
        // 4. Escribir el byte nulo final (0x00)
        WriteByte(0);
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