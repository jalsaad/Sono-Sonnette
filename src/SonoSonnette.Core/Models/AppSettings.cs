namespace SonoSonnette.Core.Models;

public sealed class AppSettings
{
    public List<ScheduleEntry> Schedules { get; set; } = new();

    /// <summary>Hash PBKDF2 du mot de passe protégeant la modification des paramètres. Null tant qu'aucun mot de passe n'a été défini.</summary>
    public string? PasswordHash { get; set; }

    public string? PasswordSalt { get; set; }

    /// <summary>Identifiant du périphérique de sortie audio (MMDevice.ID). Null = périphérique par défaut du système.</summary>
    public string? OutputDeviceId { get; set; }

    public bool StartWithWindows { get; set; } = true;

    public double Volume { get; set; } = 1.0;
}
