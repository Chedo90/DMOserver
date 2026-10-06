namespace dmoserver.game.Network;

using System;
using System.Buffers.Binary;
using System.Text;

public sealed class GamePacketReader
{
    public const short CheckSumValidation = 6716;

    private readonly byte[] _buffer;
    private int _position;
    private readonly int _actualLength;

    public short Length { get; }
    public short Type { get; }

    public GamePacketReader(byte[] packet, int actualLength)
    {
        _buffer = packet;
        _actualLength = actualLength;

        // 1. Cabecera DMO: Length (2B) + Type (2B)
        Length = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(0, 2));
        Type = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(2, 2));

        // 2. Validación de Checksum al final del paquete
        if (Length >= 6 && Length <= actualLength)
        {
            short checksum = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(Length - 2, 2));
            if (checksum != (short)(Length ^ CheckSumValidation))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] Checksum inválido recibido para Opcode {Type}");
                Console.ResetColor();
            }
        }

        // El payload de datos comienza en el byte 4
        _position = 4;
    }

    public void Seek(int position) => _position = position;
    public void Skip(int bytes) => _position += bytes;

    public byte ReadByte() => _buffer[_position++];
    public short ReadShort() => BitConverter.ToInt16(_buffer, (_position += 2) - 2);
    public ushort ReadUShort() => BitConverter.ToUInt16(_buffer, (_position += 2) - 2);
    public int ReadInt() => BitConverter.ToInt32(_buffer, (_position += 4) - 4);
    public uint ReadUInt() => BitConverter.ToUInt32(_buffer, (_position += 4) - 4);
    public long ReadLong() => BitConverter.ToInt64(_buffer, (_position += 8) - 8);

    // NUEVO MÉTODO: Lee cadenas con el formato DMO [1 byte Longitud] + [Texto] + [1 byte Nulo]
    public string ReadString()
    {
        if (_position >= _actualLength) return string.Empty;

        // 1. Leer el byte de longitud inicial
        byte stringLength = _buffer[_position++];
        
        if (stringLength == 0 || _position >= _actualLength) 
        {
            if (_position < _actualLength && _buffer[_position] == 0) _position++;
            return string.Empty;
        }

        // 2. Leer la cadena exacta
        ReadOnlySpan<byte> span = _buffer.AsSpan(_position, stringLength);
        string result = Encoding.ASCII.GetString(span);
        _position += stringLength;

        // 3. Saltar el byte nulo final (0x00)
        if (_position < _actualLength && _buffer[_position] == 0) _position++;

        return result;
    }

    // MÉTODO ANTIGUO: Lee hasta encontrar un byte 0x00 (sin byte de longitud)
    public string ReadZString()
    {
        int start = _position;
        while (_position < _actualLength && _buffer[_position] != 0) _position++;

        ReadOnlySpan<byte> span = _buffer.AsSpan(start, _position - start);
        string result = Encoding.ASCII.GetString(span);

        if (_position < _actualLength && _buffer[_position] == 0) _position++;
        return result;
    }
}