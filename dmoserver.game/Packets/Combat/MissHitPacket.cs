namespace dmoserver.game.Packets.Combat;

using dmoserver.game.Network;

/// <summary>
/// Paquete 1014: Notifica un golpe fallado (MISS) en DMO. El cliente muestra el texto flotante 'MISS'
/// y reproduce el sonido de golpe fallado.
/// </summary>
public sealed class MissHitPacket
{
    public const short OpCode = 1014;

    public uint AttackerHandle { get; set; }
    public uint TargetHandle { get; set; }

    public MissHitPacket(uint attackerHandle, uint targetHandle)
    {
        AttackerHandle = attackerHandle;
        TargetHandle = targetHandle;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(OpCode);
        writer.WriteUInt(AttackerHandle);
        writer.WriteUInt(TargetHandle);
        writer.WriteShort(0);
        return writer.Serialize();
    }
}
