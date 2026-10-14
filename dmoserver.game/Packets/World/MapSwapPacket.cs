namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class MapSwapPacket
{
    private readonly string _ip;
    private readonly int _port;
    private readonly int _mapId;
    private readonly int _x;
    private readonly int _y;

    public MapSwapPacket(string ip, int port, int mapId, int x, int y)
    {
        _ip = ip;
        _port = port;
        _mapId = mapId;
        _x = x;
        _y = y;
    }

    public byte[] Serialize()
    {
        // 1. Usamos el Opcode revelado por el Dump: 1709
        using var w = new PacketWriter(1709);

        // 2. Estructura estricta del String (1 byte de longitud + string + byte nulo)
        w.WriteByte((byte)_ip.Length);
        foreach (char c in _ip)
        {
            w.WriteByte((byte)c);
        }
        w.WriteByte(0); // Terminador nulo obligatorio

        // 3. Escribimos los datos de conexión y coordenadas
        w.WriteInt(_port);
        w.WriteInt(_mapId);
        w.WriteInt(_x);
        w.WriteInt(_y);
        
        // 4. Los últimos 2 bytes del volcado ("19 1A") para evitar que falten datos
        w.WriteShort(0); // Mandamos Canal 0

        return w.Serialize();
    }
}