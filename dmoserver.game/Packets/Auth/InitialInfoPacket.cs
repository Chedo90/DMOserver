namespace dmoserver.game.Packets;

using dmoserver.game.Entities;
using dmoserver.game.Network;

public sealed class InitialInfoPacket(PlayerEntity player, string tamerName, string digimonName, int tamerModel, int digimonModel, uint tamerHandle, uint partnerHandle, int x, int y)
{
    public const short OpCode = 1003;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(OpCode);

        // 1. Tipo y Coordenadas iniciales
        writer.WriteInt(1);
        writer.WriteInt(x); // Location X
        writer.WriteInt(y); // Location Y
        writer.WriteUInt(tamerHandle); // <--- Handle dinámico
        writer.WriteInt(tamerModel);
        writer.WriteString(tamerName);

        // 2. Stats y Atributos del Tamer
        writer.WriteLong(player.TamerCurrentExp * 100);    // CurrentExperience * 100
        writer.WriteShort((short)player.TamerLevel);       // Level (short en DMO)
        writer.WriteInt(1000);  // HP
        writer.WriteInt(500);   // DS
        writer.WriteInt(1000);  // CurrentHp
        writer.WriteInt(500);   // CurrentDs
        writer.WriteInt(0);     // Fatigue
        writer.WriteInt(100);   // AT
        writer.WriteInt(50);    // DE
        writer.WriteInt(600);   // MS

        // Helper local para escribir slots de 68 bytes vacíos
        static void WriteEmptyItemSlots(PacketWriter w, int count)
        {
            byte[] emptySlot = new byte[68];
            for (int i = 0; i < count; i++)
            {
                for (int b = 0; b < 68; b++)
                    w.WriteByte(emptySlot[b]);
            }
        }

        // 3. Equipment (13 slots x 68 bytes = 884 bytes)
        WriteEmptyItemSlots(writer, 13);

        // 4. ChipSets (12 slots x 68 bytes = 816 bytes)
        WriteEmptyItemSlots(writer, 12);

        // 5. Digivice (1 slot x 68 bytes = 68 bytes)
        WriteEmptyItemSlots(writer, 1);

        // 6. TamerSkill (5 slots x 68 bytes = 340 bytes)
        WriteEmptyItemSlots(writer, 5);

        // 7. Character Progress (768 bytes completadas + 20 * 7 bytes activas = 908 bytes)
        for (int i = 0; i < 768; i++) writer.WriteByte(0);
        for (int i = 0; i < 20; i++)
        {
            writer.WriteShort(0); // QuestId
            for (int q = 0; q < 5; q++) writer.WriteByte(0); // Quest Condition Goals
        }

        // 8. Incubator
        writer.WriteInt(0);  // EggId
        writer.WriteInt(0);  // HatchLevel
        writer.WriteInt(-1); // Egg TradeLimitTime
        writer.WriteInt(0);  // BackupDiskId
        writer.WriteInt(-1); // BackupDisk TradeLimitTime

        // 9. Tamer Buffs (0)
        writer.WriteShort(0);

        // 10. Partner Digimon Base
        writer.WriteByte(1);     // DigimonSlots
        writer.WriteUInt(partnerHandle); // <--- Handle dinámico
        writer.WriteInt(digimonModel); // CurrentType
        writer.WriteString(digimonName);
        byte hatchGrade = player.Character?.Partner?.HatchGrade ?? 3;
        short partnerSize = player.Character?.Partner?.Size ?? 10000;
        if (partnerSize <= 0) partnerSize = 10000;

        writer.WriteByte(hatchGrade);  // HatchGrade
        writer.WriteShort(partnerSize);// Size (10000 = 100.00%)
        writer.WriteLong(player.CurrentExp * 100);     // CurrentExperience * 100
        writer.WriteLong(0);     // TranscendenceExperience
        writer.WriteShort((short)player.PartnerLevel); // Level (short)
        writer.WriteInt(player.PartnerStats.MaxHp);    // HP
        writer.WriteInt(player.PartnerStats.MaxDs);    // DS
        writer.WriteInt(player.PartnerStats.Defense);  // DE
        writer.WriteInt(player.PartnerStats.Attack);   // AT
        writer.WriteInt(player.PartnerStats.CurrentHp);// CurrentHp
        writer.WriteInt(player.PartnerStats.CurrentDs);// CurrentDs
        writer.WriteInt(player.PartnerStats.Friendship); // FS (Amistad: 0 a 100)
        writer.WriteInt(0);      // ?
        writer.WriteInt((int)(player.PartnerStats.EvasionRate * 100)); // EV
        writer.WriteInt((int)(player.PartnerStats.CriticalRate * 100)); // CC (Tasa Crítico)
        writer.WriteInt(player.PartnerStats.MoveSpeed > 0 ? player.PartnerStats.MoveSpeed : 600); // MS
        writer.WriteInt(player.PartnerStats.AttackSpeed); // AS
        writer.WriteInt(0);      // ?
        writer.WriteInt(player.PartnerStats.HitRate);     // HT
        writer.WriteInt(0);      // ?
        writer.WriteInt(0);      // ?
        writer.WriteInt(player.PartnerStats.AttackRange); // AR
        writer.WriteInt((int)(player.PartnerStats.BlockRate * 100)); // BL (Tasa Bloqueo)
        writer.WriteInt(digimonModel); // BaseType

        // 11. Evolutions (1 forma x 19 bytes)
        writer.WriteByte(1); // 1 forma evolutiva

        // Experiencia y Maestría combinadas según ODMO / DMO oficial: (Mastery << 26) | Experience
        int expVal = ((player.SkillMastery & 0x3F) << 26) | (player.SkillExperience & 0x3FFFFFF);
        writer.WriteInt(expVal);

        writer.WriteByte(1); // Unlocked = 1
        writer.WriteByte(0); // Pad
        writer.WriteByte(0); // Pad
        writer.WriteByte(0); // Pad
        writer.WriteByte(player.SkillPoints); // Puntos de habilidad disponibles

        // Niveles actuales de habilidad (F1 a F5)
        writer.WriteByte(player.GetSkillLevel(1)); // F1
        writer.WriteByte(player.GetSkillLevel(2)); // F2
        writer.WriteByte(player.GetSkillLevel(3)); // F3
        writer.WriteByte(player.GetSkillLevel(4)); // F4
        writer.WriteByte(player.GetSkillLevel(5)); // F5

        // Niveles máximos (F1 a F5)
        writer.WriteByte(10); // F1 Max
        writer.WriteByte(10); // F2 Max
        writer.WriteByte(10); // F3 Max
        writer.WriteByte(10); // F4 Max
        writer.WriteByte(10); // F5 Max

        // 12. Digiclone Stats (15 shorts = 30 bytes)
        for (int i = 0; i < 15; i++) writer.WriteShort(0);

        // 13. Partner Buffs (0)
        writer.WriteShort(0);

        // 14. Attribute Experience (13 shorts = 26 bytes)
        for (int i = 0; i < 13; i++) writer.WriteShort(0);

        writer.WriteInt(0); // nUID
        writer.WriteByte(0);// CashSkillCount

        // 15. Digimons Activos (Terminador centinela 99)
        writer.WriteByte(99);

        writer.WriteInt(0); // ?
        writer.WriteInt(0); // Channel

        // 16. Map Region (192 bytes explorados / desbloqueados)
        for (int i = 0; i < 192; i++) writer.WriteByte(0);

        // 17. Digimon Archive Slots
        writer.WriteInt(1);

        // 18. Party Info
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(99);

        // 19. Current Title
        writer.WriteShort(0);

        // 20. Item Cooldowns (32 ints)
        for (int i = 0; i < 32; i++) writer.WriteInt(0);

        // 21. Eventos y Asistencia
        writer.WriteInt(0); // Game version
        writer.WriteInt(2); // nWorkDayHistory
        writer.WriteInt(0); // nTodayAttendanceTimeTS
        writer.WriteInt(0); // Alive Boss Id
        writer.WriteByte(0);// PC Bang

        // 22. Consigned Shop (0 = no shop)
        writer.WriteInt(0);

        writer.WriteInt(0);  // Client Option / Tutorial
        writer.WriteInt(0);  // Achievement rank
        writer.WriteByte(0); // Minigame flag
        writer.WriteShort(0);// Minigame success

        // 23. Tamer Skills activas y Cash buffs (0)
        writer.WriteByte(0); // Buffs.Count = 0
        writer.WriteByte(0); // CashBuffs.Count = 0

        // 24. Chat Block, Master Match, Deck Buff, Megaphone
        writer.WriteByte(0); // Block chat
        writer.WriteByte(0); // Master match
        writer.WriteByte(0); // DeckBuffId (null -> byte 0)
        writer.WriteByte(0); // Megaphone ban
        writer.WriteInt(0);  // ?

        // 25. Padding final obligatorio de 29 bytes
        for (int i = 0; i < 29; i++) writer.WriteByte(0);

        return writer.Serialize();
    }
}