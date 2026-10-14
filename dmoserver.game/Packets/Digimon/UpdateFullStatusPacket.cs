namespace dmoserver.game.Packets;

using dmoserver.game.Entities;
using dmoserver.game.Network;

/// <summary>
/// Actualiza la hoja completa de estadísticas del Tamer y del Digimon Partner en el cliente (Ventana C / HUD).
/// Opcode oficial 1043 (UpdateStatusPacket en ODMO / DMO).
/// </summary>
public class UpdateFullStatusPacket
{
    private readonly PlayerEntity _player;

    public short Opcode => 1043;

    public UpdateFullStatusPacket(PlayerEntity player)
    {
        _player = player;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(Opcode);

        // --- 1. STATS DEL TAMER ---
        int tamerHp = 1000 + (_player.TamerLevel * 20);
        int tamerDs = 800 + (_player.TamerLevel * 10);
        int tamerAt = 50 + (_player.TamerLevel * 2);
        int tamerDe = 20 + _player.TamerLevel;
        short tamerMs = 600;

        writer.WriteInt(tamerHp);          // Tamer Max HP
        writer.WriteInt(tamerDs);          // Tamer Max DS
        writer.WriteInt(tamerHp);          // Tamer Current HP
        writer.WriteInt(tamerDs);          // Tamer Current DS
        writer.WriteShort((short)tamerAt); // Tamer AT
        writer.WriteShort((short)tamerDe); // Tamer DE
        writer.WriteShort(tamerMs);        // Tamer MS (Movement Speed)

        // --- 2. STATS DEL DIGIMON PARTNER ---
        var stats = _player.PartnerStats;
        int partnerHp = Math.Max(1, stats.MaxHp);
        int partnerDs = Math.Max(1, stats.MaxDs);
        int partnerCurHp = Math.Clamp(stats.CurrentHp, 0, partnerHp);
        int partnerCurDs = Math.Clamp(stats.CurrentDs, 0, partnerDs);

        writer.WriteInt(partnerHp);                              // Partner Max HP
        writer.WriteInt(partnerDs);                              // Partner Max DS
        writer.WriteInt(partnerCurHp);                           // Partner Current HP
        writer.WriteInt(partnerCurDs);                           // Partner Current DS
        writer.WriteShort((short)Math.Clamp(stats.Friendship, 0, 100)); // Friendship (FS)
        writer.WriteShort((short)stats.Attack);                  // AT
        writer.WriteShort((short)stats.Defense);                 // DE
        writer.WriteShort((short)(stats.CriticalRate * 100));    // CC / CT (formato DMO x100)
        writer.WriteShort((short)stats.AttackSpeed);             // AS
        writer.WriteShort((short)(stats.EvasionRate * 100));     // EV (formato DMO x100)
        writer.WriteShort((short)stats.HitRate);                 // HT
        writer.WriteShort((short)stats.AttackRange);             // AR
        writer.WriteShort((short)(stats.BlockRate * 100));       // BL

        // --- 3. DIGICLONE STATS & LEVELS (14 shorts) ---
        // Clone Values: Level, AT, BL, CT, AS, EV, HT, HP
        writer.WriteShort(0); // Clone Level
        writer.WriteShort(0); // AT Value
        writer.WriteShort(0); // BL Value
        writer.WriteShort(0); // CT Value
        writer.WriteShort(0); // AS
        writer.WriteShort(0); // EV Value
        writer.WriteShort(0); // HT
        writer.WriteShort(0); // HP Value

        // Clone Levels: AT, BL, CT, AS, EV, HT, HP
        writer.WriteShort(0); // AT Level
        writer.WriteShort(0); // BL Level
        writer.WriteShort(0); // CT Level
        writer.WriteShort(0); // AS
        writer.WriteShort(0); // EV Level
        writer.WriteShort(0); // HT
        writer.WriteShort(0); // HP Level

        return writer.Serialize();
    }
}
