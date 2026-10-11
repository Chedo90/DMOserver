namespace dmoserver.game.Handlers;

using dmoserver.game.Network;
using dmoserver.game.Packets;
using System;
using System.Threading.Tasks;

public static class CombatHandler
{
    public static async Task HandleAttackRequestAsync(GameClient client, GamePacketReader packet)
    {
        uint attackerHandle = packet.ReadUInt();
        uint targetHandle = packet.ReadUInt();
        short animationExtra = packet.ReadShort();

        // ==========================================
        // 1. LOG DE DIAGNÓSTICO INICIAL
        // ==========================================
        Console.WriteLine($"[DEBUG] Petición 1013 recibida | Atacante: {attackerHandle:X} | Target: {targetHandle:X} | Mapa Asignado: {client.CurrentMap != null}");

        var targetMonster = client.CurrentMap?.GetMonster(targetHandle);
        
        if (targetMonster == null)
        {
            Console.WriteLine($"[DEBUG] ERROR: El monstruo con Handle {targetHandle:X} no existe en la memoria del mapa.");
            return; 
        }

        if (targetMonster.IsDead)
        {
            Console.WriteLine($"[DEBUG] Ignorado: El monstruo {targetHandle:X} ya está marcado como muerto.");
            return;
        }

        // ==========================================
        // 2. CÁLCULO DE DAÑO BASADO EN STATS REALES
        // ==========================================
        // Leemos el AT real del Partner del jugador (fallback a 100 si client.Player fuera nulo)
        int attackerAtk = client.Player?.PartnerStats.Attack ?? 100;
        int targetDef = targetMonster.Stats.Defense;

        // Fórmula: AT - DE (mínimo 1 de daño garantizado)
        int baseDamage = Math.Max(1, attackerAtk - targetDef);
        
        // Variación aleatoria del daño (+/- 5)
        int variation = Random.Shared.Next(-5, 6);
        int damage = Math.Max(1, baseDamage + variation);
        // ==========================================

        bool isKilled = targetMonster.TakeDamage(damage);

        Console.ForegroundColor = isKilled ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.WriteLine($"[⚔️ Combate] {attackerHandle:X} ataca a {targetHandle:X} por {damage} (AT: {attackerAtk} vs DE: {targetDef}). Muerto: {isKilled}");
        Console.ResetColor();

        if (isKilled)
        {
            var killPacket = new KillOnHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = 0
            };

            client.CurrentMap.Broadcast(killPacket.Serialize());
            
            targetMonster.NextActionTime = DateTime.UtcNow.AddSeconds(7); // Respawn en 7s
        }
        else
        {
            var hitPacket = new CombatHitPacket
            {
                AttackerHandle = attackerHandle,
                TargetHandle = targetHandle,
                Damage = damage,
                HitType = 0,
                TargetHp = targetMonster.CurrentHp,
                TargetMaxHp = targetMonster.Stats.MaxHp
            };

            client.CurrentMap.Broadcast(hitPacket.Serialize());
        }
    }
}