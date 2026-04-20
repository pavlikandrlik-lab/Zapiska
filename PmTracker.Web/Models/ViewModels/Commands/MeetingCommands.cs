using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels;

public sealed class SaveMeetingCommand
{
    public int? Id { get; set; }

    [Required]
    public int ProjektId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Číslo jednání musí být větší než 0.")]
    public int CisloJednani { get; set; }

    [Required]
    public DateTime DatumPlanovane { get; set; }

    [Required]
    public TimeOnly CasZacatek { get; set; }

    public string? Misto { get; set; }

    [Required]
    public string StavJednani { get; set; } = string.Empty;
}

public sealed class DeleteMeetingCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int ProjektId { get; set; }
}

public sealed class SaveMeetingStatusCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public string Stav { get; set; } = string.Empty;

    public bool UzavritJednani { get; set; }

    public bool OtevritJednani { get; set; }
}

public sealed class SaveMeetingNoteCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class SaveAttendanceCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int OsobaId { get; set; }

    [Required]
    public string StavUcasti { get; set; } = string.Empty;
}

public sealed class AddMeetingParticipantCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int JednaniId { get; set; }

    [Required(ErrorMessage = "Vyberte osobu z nabídky.")]
    public int? OsobaId { get; set; }
}

public sealed class AssignMeetingIdentifierCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public int JednaniId { get; set; }
}
