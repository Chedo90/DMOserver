namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class UpdateStatusPacket
{
    private readonly uint _handle;
    private readonly int _currentHp;
    private readonly int _maxHp;
    private readonly int _currentDs;
    private readonly int _maxDs;

    public UpdateStatusPacket(uint handle, int currentHp, int maxHp, int currentDs = 500, int maxDs = 500)
    {
        _handle = handle;
        _currentHp = currentHp;
        _maxHp = maxHp;
        _currentDs = currentDs;
        _maxDs = maxDs;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1039);

        // Handle de la entidad (Tamer o Partner)
        writer.WriteUInt(_handle);

        // Puntos de Salud (HP)
        writer.WriteInt(_currentHp);
        writer.WriteInt(_maxHp);

        // Puntos de DigiSoul (DS)
        writer.WriteInt(_currentDs);
        writer.WriteInt(_maxDs);

        return writer.Serialize();
    }
}