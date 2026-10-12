namespace dmoserver.game.Entities.Stats;

using System; // Necesario para Math.Max

public class DigimonStats
{
    // Nivel
    public byte Level { get; set; } = 1;

    // Recursos vitales
    public int MaxHp { get; set; } = 500;
    public int MaxDs { get; set; } = 200;
    
    // --- AÑADIDO: El DS actual del Digimon ---
    public int CurrentDs { get; set; } = 200; 

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

    // Experiencia que configuraremos en la base de datos
    public long TamerExpReward { get; set; }
    public long DigimonExpReward { get; set; }

    // --- AÑADIDO: Método para gastar el DS ---
    public void UseDs(int amount)
    {
        if (amount <= 0) return;
        
        CurrentDs = Math.Max(0, CurrentDs - amount);
    }
}