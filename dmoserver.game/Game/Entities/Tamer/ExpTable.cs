namespace dmoserver.game.Entities.Stats;

using System;
using System.Collections.Generic;

/// <summary>
/// Exp necesaria para pasar de 'level' a 'level + 1', en la MISMA unidad que usa el servidor
/// (antes del * 100 de ExpUpdatePacket). Los valores medidos salen de comparar el porcentaje
/// que dibuja el cliente con la exp sobrante que mandaba el servidor.
/// </summary>
public static class ExpTable
{
    // ===== VALORES MEDIDOS (confirmados con capturas) =====
    private static readonly Dictionary<int, long> DigimonMeasured = new()
    {
        { 3, 250 }, { 4, 280 }, { 5, 314 }, { 6, 352 }
    };

    private static readonly Dictionary<int, long> TamerMeasured = new()
    {
        { 2, 437 }, { 3, 996 }, { 4, 1792 }, { 5, 2835 }
    };

    // ===== ESTIMACIONES (sin medir todavia) =====
    // Digimon: crece ~x1.121 por nivel. Tamer: la diferencia entre niveles sube ~+250 por nivel.
    private const double DigimonGrowth = 1.121;
    private const long TamerDiffStep = 250;

    public static long DigimonNeeded(int level)
    {
        if (level < 1) level = 1;

        if (DigimonMeasured.TryGetValue(level, out var known))
            return known;

        double value;
        if (level < 3)
        {
            // Hacia atras desde el nivel 3: L2 ~ 223, L1 ~ 199
            value = 250;
            for (int l = 3; l > level; l--)
                value /= DigimonGrowth;
        }
        else
        {
            // Hacia adelante desde el nivel 6
            value = 352;
            for (int l = 6; l < level; l++)
                value *= DigimonGrowth;
        }

        return Math.Max(1, (long)Math.Round(value));
    }

    public static long TamerNeeded(int level)
    {
        if (level < 1) level = 1;

        if (TamerMeasured.TryGetValue(level, out var known))
            return known;

        if (level < 2)
            return 105; // estimacion para el nivel 1

        // Hacia adelante desde el nivel 5
        long need = 2835;
        long diff = 2835 - 1792;
        for (int l = 5; l < level; l++)
        {
            diff += TamerDiffStep;
            need += diff;
        }

        return Math.Max(1, need);
    }
}