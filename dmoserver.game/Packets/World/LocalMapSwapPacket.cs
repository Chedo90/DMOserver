namespace dmoserver.game.Packets;

using dmoserver.game.Network;

/// <summary>
/// Opcode 1711: Teletransporte local en el mismo mapa.
/// Reposiciona instantáneamente al Tamer y a su Digimon compañero sin pantalla de carga.
/// </summary>
public sealed class LocalMapSwapPacket
{
    private readonly int _tamerHandler;
    private readonly int _partnerHandler;
    private readonly int _tamerX;
    private readonly int _tamerY;
    private readonly int _partnerX;
    private readonly int _partnerY;

    public LocalMapSwapPacket(int tamerHandler, int partnerHandler, int tamerX, int tamerY, int partnerX, int partnerY)
    {
        _tamerHandler = tamerHandler;
        _partnerHandler = partnerHandler;
        _tamerX = tamerX;
        _tamerY = tamerY;
        _partnerX = partnerX;
        _partnerY = partnerY;
    }

    public byte[] Serialize()
    {
        using var w = new PacketWriter(1711);
        w.WriteInt(_tamerHandler);
        w.WriteInt(_partnerHandler);
        w.WriteInt(_tamerX);
        w.WriteInt(_tamerY);
        w.WriteInt(_partnerX);
        w.WriteInt(_partnerY);
        return w.Serialize();
    }
}
