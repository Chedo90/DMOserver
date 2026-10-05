using dmoserver.login.Network;

namespace dmoserver.login.Packet;

public static class ServerListPacket
{
    public static byte[] Create()
    {
        // Opcode 1701: Lista de Servidores
        var writer = new PacketWriter(1701);

        // 1. Cantidad de servidores disponibles
        writer.WriteByte(1);

        // --- Servidor 1 ---
        writer.WriteInt(1);                   // ID del servidor
        writer.WriteString("Localhost");      // Nombre
        writer.WriteByte(0);                  // Mantenimiento (0 = Online)
        writer.WriteByte(0);                  // Carga (0 = Normal)
        writer.WriteByte(0);                  // Personajes existentes en este server
        writer.WriteByte(1);                  // Etiqueta "New" (1 = Sí)

        // 2. Terminador de la lista
        writer.WriteInt(0);

        return writer.Build();
    }
}