namespace dmoserver.game.Entities.Stats;

using System;

/// <summary>
/// Tabla oficial de experiencia de Digimon Masters Online (DMO / ODMO).
/// Define la experiencia necesaria para pasar de 'level' a 'level + 1'.
/// Extraída directamente de la base de datos oficial (Asset.DigimonLevelStatus y Asset.CharacterLevelStatus).
/// </summary>
public static class ExpTable
{
    // =========================================================================
    // TABLA OFICIAL DE EXPERIENCIA PARA DIGIMON (Niveles 1 a 120)
    // Fuente: Asset.DigimonLevelStatus (ScaleType = 1 / Agumon)
    // =========================================================================
    private static readonly long[] OfficialDigimonExp = new long[]
    {
        200L, 224L, 251L, 281L, 315L, 352L, 395L, 442L, 495L, 555L,
        621L, 696L, 779L, 873L, 977L, 1095L, 1226L, 1373L, 1538L, 1723L,
        1929L, 2161L, 2420L, 2710L, 3036L, 3400L, 3808L, 4265L, 4777L, 5350L,
        5992L, 6711L, 7516L, 8418L, 9429L, 10560L, 11827L, 13246L, 14836L, 16616L,
        18610L, 20843L, 23345L, 26146L, 29284L, 32798L, 36733L, 41141L, 46078L, 51608L,
        57800L, 64736L, 72505L, 81205L, 90950L, 101864L, 114088L, 127778L, 143112L, 160285L,
        179519L, 201062L, 225189L, 252212L, 282477L, 316374L, 354339L, 396860L, 444483L, 497821L,
        557560L, 624467L, 699403L, 783332L, 877331L, 982611L, 1100525L, 1232587L, 1380498L, 1546158L,
        1731697L, 1939500L, 2172240L, 2432909L, 2724858L, 3051841L, 3418062L, 3828230L, 4287617L, 4802131L,
        5378387L, 6023793L, 6746648L, 7556246L, 8462996L, 9478555L, 10615982L, 11889900L, 13316688L, 14914690L,
        16704453L, 18708988L, 20954066L, 23468554L, 26284780L, 29438954L, 32971629L, 36928224L, 41359611L, 46322764L,
        51881496L, 58107275L, 65080148L, 72889766L, 81636538L, 91432923L, 102404873L, 114693458L, 128456673L, 143871474L
    };

    // =========================================================================
    // TABLA OFICIAL DE EXPERIENCIA PARA TAMER (Niveles 1 a 120)
    // Fuente: Asset.CharacterLevelStatus (Type = 80001 / Marcus Damon)
    // =========================================================================
    private static readonly long[] OfficialTamerExp = new long[]
    {
        135L, 437L, 996L, 1792L, 2835L, 4131L, 5688L, 7516L, 9622L, 12015L,
        14701L, 17690L, 20989L, 24607L, 28552L, 32832L, 37454L, 42427L, 47760L, 53460L,
        66083L, 73180L, 80698L, 88646L, 97030L, 105861L, 115145L, 124891L, 135106L, 218700L,
        285417L, 359424L, 441045L, 546210L, 661500L, 787320L, 924075L, 1072170L, 1232010L, 1404000L,
        1588545L, 1786050L, 1996920L, 2221560L, 2460375L, 2713770L, 2982150L, 3265920L, 3565485L, 3881250L,
        4213620L, 4563000L, 4929795L, 5314410L, 5717250L, 6138720L, 6579225L, 7039170L, 7518960L, 8019000L,
        8539695L, 9081450L, 9644670L, 10229760L, 10837125L, 11467170L, 12120300L, 12796920L, 13497435L, 14222250L,
        14971770L, 15746400L, 16546545L, 17372610L, 18225000L, 19104120L, 20010375L, 20944170L, 21905910L, 22896000L,
        23914845L, 24962850L, 26040420L, 27147960L, 28285875L, 29454570L, 30654450L, 31885920L, 33149385L, 34445250L,
        34445250L, 34445250L, 34445250L, 34445250L, 34445250L, 34445250L, 34445250L, 34445250L, 41735970L, 44735970L,
        47857780L, 51105070L, 54481590L, 57991160L, 61637670L, 65425100L, 69357480L, 73438940L, 98847400L, 103239700L,
        107793910L, 112514460L, 117405880L, 122472760L, 127719770L, 133151680L, 138773340L, 144589670L, 180685770L, 186906570L
    };

    /// <summary>
    /// Retorna la cantidad de experiencia exacta necesaria para que el Digimon suba de 'level' a 'level + 1'.
    /// </summary>
    public static long DigimonNeeded(int level)
    {
        if (level < 1) level = 1;
        if (level <= OfficialDigimonExp.Length)
            return OfficialDigimonExp[level - 1];

        return OfficialDigimonExp[^1];
    }

    /// <summary>
    /// Retorna la cantidad de experiencia exacta necesaria para que el Tamer suba de 'level' a 'level + 1'.
    /// </summary>
    public static long TamerNeeded(int level)
    {
        if (level < 1) level = 1;
        if (level <= OfficialTamerExp.Length)
            return OfficialTamerExp[level - 1];

        return OfficialTamerExp[^1];
    }
}