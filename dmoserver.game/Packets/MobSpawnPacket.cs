namespace dmoserver.game.Packets;

using dmoserver.game.Network;

public class MobSpawnPacket
{
    private readonly uint _handle;
    private readonly int _monsterId;
    private readonly int _x;
    private readonly int _y;

    public MobSpawnPacket(uint handle, int monsterId, int x, int y)
    {
        _handle = handle;
        _monsterId = monsterId;
        _x = x;
        _y = y;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter(1006);

        // Acción: 1 = Spawn (Aparecer de la nada), 3 = Load (Entrar en el rango de visión)
        writer.WriteByte(1); 
        
        // Cantidad de monstruos en este paquete
        writer.WriteShort(1);

        // ---------------- DATOS DEL MONSTRUO ----------------
        // Previous Location X e Y
        writer.WriteInt(_x);
        writer.WriteInt(_y);

        // GeneralHandler (ID único en memoria)
        writer.WriteInt((int)_handle);

        // Type / MonsterID (El ID real del monstruo, ej: 30419)
        writer.WriteInt(_monsterId);

        // Current Location X e Y
        writer.WriteInt(_x);
        writer.WriteInt(_y);

        // CurrentHpRate (Porcentaje de vida al 100%)
        writer.WriteByte(100);

        // Level (Nivel del monstruo)
        writer.WriteShort(1);

        // Skill Idx (2 por defecto en el emulador antiguo)
        writer.WriteShort(2);

        // GrowStack (0 = Escala normal)
        writer.WriteInt(0);

        // SyncCondition (0 = NormalState)
        writer.WriteInt(0);

        // DisposedObjects
        writer.WriteInt(0);

        // Padding final de la entidad
        writer.WriteByte(0);
        // ----------------------------------------------------

        // Cierre del paquete
        writer.WriteInt(0);

        return writer.Serialize();
    }
}