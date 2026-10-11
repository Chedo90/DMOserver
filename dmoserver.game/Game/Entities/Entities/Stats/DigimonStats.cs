namespace dmoserver.game.Entities.Stats;

public class DigimonStats
{
    // Nivel
    public byte Level { get; set; } = 1;

    // Recursos vitales
    public int MaxHp { get; set; } = 500;
    public int MaxDs { get; set; } = 200;

    // Ofensivos
    public int Attack { get; set; } = 120;       // AT
    public int AttackSpeed { get; set; } = 2000; // AS (milisegundos)
    public float CriticalRate { get; set; } = 5.0f; // CT (%)
    public int HitRate { get; set; } = 500;      // HT

    // Defensivos
    public int Defense { get; set; } = 35;       // DE
    public float BlockRate { get; set; } = 0.0f; // BL (%)
    public float EvasionRate { get; set; } = 0.0f; // EV (%)

    // Recompensa al morir (solo mobs)
    public int ExpReward { get; set; } = 50;
}