namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public sealed class LoadTamerPacket(string tamerName, string digimonName, int tamerModel, int digimonModel)
{
    public const short OpCode = 1006;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(OpCode);

        writer.WriteByte(3);
        writer.WriteShort(2);

        // Coordenadas y datos básicos del Tamer
        writer.WriteInt(30000); // X
        writer.WriteInt(30000); // Y
        writer.WriteUInt(1000); // GeneralHandler
        writer.WriteInt(tamerModel);
        writer.WriteInt(30000); // X dup
        writer.WriteInt(30000); // Y dup
        writer.WriteString(tamerName);
        writer.WriteByte(1);     // Level
        writer.WriteFloat(0.0f); // Z
        writer.WriteShort(600);  // MS
        writer.WriteByte(100);   // HpRate

        // Helper para serializar slots vacíos de 68 bytes
        static void WriteEmptyItemSlots(PacketWriter w, int count)
        {
            byte[] emptySlot = new byte[68];
            for (int i = 0; i < count; i++)
            {
                for (int b = 0; b < 68; b++)
                    w.WriteByte(emptySlot[b]);
            }
        }

        // 1. Equipment: 13 slots * 68 bytes = 884 bytes
        WriteEmptyItemSlots(writer, 13);

        // 2. Digivice: 1 slot * 68 bytes = 68 bytes
        WriteEmptyItemSlots(writer, 1);

        writer.WriteInt(0); // CurrentCondition (0 = normal)
        writer.WriteInt(0); // Sync
        writer.WriteInt(2000); // Partner GeneralHandler
        writer.WriteShort(10000); // Size (100.00%)

        // Guild (null -> byte 0)
        writer.WriteByte(0);

        writer.WriteShort(0); // CurrentTitle
        writer.WriteByte(0);  // Master match team
        writer.WriteShort(0); // SealLeaderId

        // ShopName solo se escribe si condition == TamerShop (aquí es 0, no se escribe)
        writer.WriteInt(0);   // Costume

        // Datos del Partner Digimon
        writer.WriteInt(30050); // Partner X
        writer.WriteInt(30050); // Partner Y
        writer.WriteInt(2000);  // Partner GeneralHandler
        writer.WriteInt(digimonModel); // CurrentType
        writer.WriteInt(30050); // Partner X dup
        writer.WriteInt(30050); // Partner Y dup
        writer.WriteString(digimonName);
        writer.WriteShort(10000); // Size
        writer.WriteByte(1);      // Level
        writer.WriteFloat(0.0f);  // Z
        writer.WriteShort(600);   // MS
        writer.WriteShort(2000);  // AS
        writer.WriteUInt(1000);   // Tamer GeneralHandler
        writer.WriteByte(100);    // HpRate
        writer.WriteInt(0);       // Partner Condition

        // Digiclone del Partner (10 shorts)
        writer.WriteShort(0); // CloneLevel
        writer.WriteShort(0); // ATLevel
        writer.WriteShort(0); // BLLevel
        writer.WriteShort(0); // CTLevel
        writer.WriteShort(0); // AS
        writer.WriteShort(0); // EVLevel
        writer.WriteShort(0); // HT
        writer.WriteShort(0); // HPLevel
        writer.WriteShort(0); // ?
        writer.WriteShort(0); // ?

        writer.WriteShort(0); // Padding / byte final

        return writer.Serialize();
    }
}