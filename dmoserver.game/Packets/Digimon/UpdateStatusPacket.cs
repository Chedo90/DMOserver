namespace dmoserver.game.Packets;

using dmoserver.game.Network;

/// <summary>
/// Actualiza los recursos actuales (HP / DS / fatiga) de una entidad.
/// Opcode 1023 (UpdateCurrentResourcesPacket en Oldserver).
/// OJO: el 1039 NO es válido para esto (es HATCH_REMOVE_EGG) y el cliente lanza
/// "unknown protocol(1039)".
/// </summary>
public class UpdateStatusPacket
{
    private readonly uint _handle;
    private readonly int _currentHp;
    private readonly int _currentDs;
    private readonly int _fatigue;

    // maxHp / maxDs se aceptan por compatibilidad con las llamadas existentes,
    // pero este paquete solo transporta los valores actuales.
    public UpdateStatusPacket(uint handle, int currentHp, int maxHp, int currentDs = 500, int maxDs = 500, int fatigue = 0)
    {
        _handle = handle;
        _currentHp = currentHp;
        _currentDs = currentDs;
        _fatigue = fatigue;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1023);

        writer.WriteUInt(_handle);
        writer.WriteShort((short)_currentHp);
        writer.WriteShort((short)_currentDs);
        writer.WriteShort((short)_fatigue);

        return writer.Serialize();
    }
}