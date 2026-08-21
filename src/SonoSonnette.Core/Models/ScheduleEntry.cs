namespace SonoSonnette.Core.Models;

public sealed class ScheduleEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string AudioFilePath { get; set; } = string.Empty;

    /// <summary>Heure de déclenchement (heures/minutes, secondes ignorées).</summary>
    public TimeOnly TimeOfDay { get; set; }

    /// <summary>Jours de la semaine où ce planning est actif. Un jour absent = pas de déclenchement ce jour-là.</summary>
    public HashSet<DayOfWeek> ActiveDays { get; set; } = new(Enum.GetValues<DayOfWeek>());

    public bool Enabled { get; set; } = true;

    public bool IsActiveOn(DayOfWeek day) => ActiveDays.Contains(day);
}
