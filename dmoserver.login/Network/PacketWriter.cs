using System.Text;

namespace dmoserver.login.Network;

public class PacketWriter
{
    private const short CheckSumValidation = 6716;
    private readonly List<byte> _body = new();
    private readonly short _opcode;

    public PacketWriter(short opcode)
    {
        _opcode = opcode;
    }

    public void WriteByte(byte value) => _body.Add(value);
    public void WriteShort(short value) => _body.AddRange(BitConverter.GetBytes(value));
    public void WriteInt(int value) => _body.AddRange(BitConverter.GetBytes(value));
    public void WriteUInt(uint value) => _body.AddRange(BitConverter.GetBytes(value));

    // Texto dinámico con longitud (para Login)
    public void WriteString(string value)
    {
        byte[] textBytes = Encoding.ASCII.GetBytes(value);
        _body.Add((byte)textBytes.Length);
        _body.AddRange(textBytes);
        _body.Add(0);
    }

    // Texto de tamaño fijo sin longitud (para la IP del Opcode 901)
    public void WriteFixedString(string value, int fixedSize)
    {
        byte[] textBytes = Encoding.ASCII.GetBytes(value);
        byte[] buffer = new byte[fixedSize];
        
        // Copiamos los bytes del texto asegurándonos de que ocupe exactamente 'fixedSize' rellenando con ceros
        Array.Copy(textBytes, buffer, Math.Min(textBytes.Length, fixedSize));
        _body.AddRange(buffer);
    }

    public byte[] Build()
    {
        short totalLength = (short)(_body.Count + 6);
        short checksum = (short)(totalLength ^ CheckSumValidation);

        List<byte> packet = new List<byte>(totalLength);
        packet.AddRange(BitConverter.GetBytes(totalLength));
        packet.AddRange(BitConverter.GetBytes(_opcode));
        packet.AddRange(_body);
        packet.AddRange(BitConverter.GetBytes(checksum));

        return packet.ToArray();
    }
}