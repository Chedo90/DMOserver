namespace dmoserver.game.Handlers.Combat;

using System;
using dmoserver.game.Entities.Stats;

/// <summary>
/// Motor oficial de cálculo de combate, ventajas elementales, de atributo, críticos y bloqueos según ODMO.
/// </summary>
public static class CombatCalculator
{
    /// <summary>
    /// Rueda oficial de ventajas de Atributos en DMO:
    /// Data vence a Vaccine (y None) | Vaccine vence a Virus (y None) | Virus vence a Data (y None) | Unknown vence a todos.
    /// </summary>
    public static bool HasAttributeAdvantage(int attackerAttr, int defenderAttr)
    {
        return attackerAttr switch
        {
            2 => defenderAttr == 1 || defenderAttr == 3, // Data > None, Vaccine
            3 => defenderAttr == 1 || defenderAttr == 4, // Vaccine > None, Virus
            4 => defenderAttr == 1 || defenderAttr == 2, // Virus > None, Data
            5 => true,                                   // Unknown > Todos
            _ => false
        };
    }

    /// <summary>
    /// Rueda oficial de ventajas Elementales en DMO:
    /// Fuego > Hielo > Agua > Fuego | Viento > Madera > Tierra > Viento | Luz > Oscuridad > Trueno > Acero > Luz.
    /// </summary>
    public static bool HasElementAdvantage(int attackerElem, int defenderElem)
    {
        return attackerElem switch
        {
            16 => defenderElem == 0 || defenderElem == 17, // Ice > Water
            17 => defenderElem == 0 || defenderElem == 18, // Water > Fire
            18 => defenderElem == 0 || defenderElem == 16, // Fire > Ice
            19 => defenderElem == 0 || defenderElem == 20, // Land > Wind
            20 => defenderElem == 0 || defenderElem == 21, // Wind > Wood
            21 => defenderElem == 0 || defenderElem == 19, // Wood > Land
            22 => defenderElem == 0 || defenderElem == 23, // Light > Dark
            23 => defenderElem == 0 || defenderElem == 24, // Dark > Thunder
            24 => defenderElem == 0 || defenderElem == 25, // Thunder > Steel
            25 => defenderElem == 0 || defenderElem == 22, // Steel > Light
            _ => false
        };
    }

    /// <summary>
    /// Calcula el daño final y el tipo de impacto (hitType) usando las fórmulas y porcentajes auténticos de DMO / ODMO.
    /// Retorna (daño, hitType): hitType 0 = Normal, 1 = Crítico, 2 = Bloqueado.
    /// </summary>
    public static (int damage, byte hitType) CalculateDamage(
        DigimonStats attacker,
        DigimonStats defender,
        int extraSkillPower = 0)
    {
        // 1. Daño base: AT - DE
        int baseAtk = attacker.Attack + extraSkillPower;
        int targetDef = defender.Defense;
        int baseDmg = Math.Max(1, baseAtk - targetDef);

        // Protección de daño mínimo de ODMO (si la defensa absorbe más del 50%, garantiza al menos el 60% del AT)
        if (baseDmg < (int)(baseAtk * 0.5))
        {
            baseDmg = (int)(baseAtk * 0.6);
        }

        // Variación aleatoria natural (-2 a +3)
        baseDmg += Random.Shared.Next(-2, 4);
        baseDmg = Math.Max(1, baseDmg);

        byte hitType = 0; // 0 = Normal, 1 = Crítico, 2 = Bloqueado

        // 2. Comprobación de Bloqueo (Block): divide el daño a la mitad
        bool blocked = defender.BlockRate > 0 && (Random.Shared.NextDouble() * 100 < defender.BlockRate);
        if (blocked)
        {
            baseDmg = Math.Max(1, baseDmg / 2);
            hitType = 2; // Bloqueado
        }

        // 3. Comprobación de Golpe Crítico (Critical Hit)
        double critBonus = 0.0;
        if (!blocked && attacker.CriticalRate > 0 && (Random.Shared.NextDouble() * 100 < attacker.CriticalRate))
        {
            // En ODMO: baseCd = 100% (+100% daño extra = daño x2.0)
            critBonus = baseDmg * 1.0;
            hitType = 1; // Crítico
        }

        // 4. Multiplicador de Atributo (Vaccine / Virus / Data)
        double attrMultiplier = 0.0;
        if (HasAttributeAdvantage(attacker.AttributeType, defender.AttributeType))
        {
            attrMultiplier += 0.50; // +50% de daño súper eficaz
        }
        else if (HasAttributeAdvantage(defender.AttributeType, attacker.AttributeType))
        {
            attrMultiplier -= 0.25; // -25% de daño en desventaja
        }

        // 5. Multiplicador de Elemento (Fuego / Hielo / Agua / etc.)
        double elemMultiplier = 0.0;
        if (HasElementAdvantage(attacker.NatureType, defender.NatureType))
        {
            elemMultiplier += 0.50; // +50% de daño elemental súper eficaz
        }
        else if (HasElementAdvantage(defender.NatureType, attacker.NatureType))
        {
            elemMultiplier -= 0.25; // -25% de daño elemental en desventaja
        }

        // 6. Daño final total
        double totalDmg = baseDmg + critBonus + (baseDmg * attrMultiplier) + (baseDmg * elemMultiplier);
        int finalDamage = Math.Max(1, (int)Math.Round(totalDmg));

        return (finalDamage, hitType);
    }

    /// <summary>
    /// Comprueba si un ataque físico básico falla (MISS) comparando el Hit Rate (HT) del atacante contra la Evasión (EV) del defensor.
    /// Según la lógica oficial de ODMO:
    /// - Si la diferencia de nivel es >= 15 a favor del atacante, nunca falla.
    /// - Si el Hit Rate supera la Evasión, el golpe acierta.
    /// - Si la Evasión supera el Hit Rate, se calcula la probabilidad de Miss calibrada (5% a 45%).
    /// </summary>
    public static bool CanMissHit(DigimonStats attacker, DigimonStats defender)
    {
        int levelDiff = attacker.Level - defender.Level;
        if (levelDiff >= 15) return false;

        double attackerHt = attacker.HitRate > 0 ? attacker.HitRate : 500;
        double defenderEv = defender.EvasionRate > 0 ? (defender.EvasionRate * 100) : 0;

        if (attackerHt >= defenderEv)
            return false;

        double diff = defenderEv - attackerHt;
        double missChance = Math.Clamp(diff / 25.0, 5.0, 45.0);

        return Random.Shared.NextDouble() * 100 < missChance;
    }
}
