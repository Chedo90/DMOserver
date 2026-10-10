namespace dmoserver.game.Handlers;

using System;
using System.Threading.Tasks;
using dmoserver.game.Network;

public static class CombatHandler
{
    // Manejo del paquete entrante 1013 (Petición de ataque del cliente)
    public static async Task HandleAttackRequestAsync(GameClient client, GamePacketReader packet)
    {
        // 1. Leer datos enviados por el cliente al hacer clic en el mob
        uint targetHandle = packet.ReadUInt(); // Handle del enemigo seleccionado (ej: 65706)
        
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Combate] El Tamer ordenó atacar a la entidad: {targetHandle}");
        Console.ResetColor();

        // 2. Paquete de confirmación de combate / fijar objetivo (Opcode 1020 / 0x03FC)
        using (var writer = new PacketWriter(1020))
        {
            writer.WriteUInt(client.PartnerHandle); // Quién ataca (tu Digimon compañero)
            writer.WriteUInt(targetHandle);         // Quién recibe el ataque
            writer.WriteByte(1);                    // 1 = Modo combate activo
            writer.WriteByte(0x24);                 // Terminador de combate
            writer.WriteByte(0x1A);

            await client.SendAsync(writer.Serialize());
        }

        // 3. Paquete de aplicación de daño y animación de golpe (Opcode 1013 / 0x03F5)
        int damageDealt = 150;
        int remainingMobHp = 850;

        using (var writer = new PacketWriter(1013))
        {
            writer.WriteUInt(client.PartnerHandle); // Atacante
            writer.WriteUInt(targetHandle);         // Objetivo
            writer.WriteInt(damageDealt);           // Daño infligido
            writer.WriteInt(remainingMobHp);        // Vida restante del mob
            writer.WriteByte(0);                    // Flags (Crítico, Miss, Bloqueo)
            writer.WriteByte(0x1C);                 // Terminador 1C 1A
            writer.WriteByte(0x1A);

            await client.SendAsync(writer.Serialize());
        }
    }
}