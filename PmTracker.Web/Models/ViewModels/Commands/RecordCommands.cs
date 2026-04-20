using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels;

public sealed class SaveRecordCommand
{
    public int? Id { get; set; }

    [Required]
    public int ProjektId { get; set; }

    [Required]
    public string Kategorie { get; set; } = string.Empty;

    public string? TypUkolu { get; set; }

    [Required]
    public string Stav { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Nazev { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Cil { get; set; }

    public string? Popis { get; set; }

    [Required(ErrorMessage = "Vyberte vlastníka z nabídky.")]
    public int? VlastnikId { get; set; }

    [Required]
    public DateTime DatumZalozeni { get; set; }

    [Required]
    public DateTime TerminUkonceni { get; set; }

    [Required]
    public string Subsystem { get; set; } = string.Empty;

    public int CisloZaznamu { get; set; }

    public List<int> VybraniSpolupracovniciIds { get; set; } = new();

    public List<SaveRecordExterniVazbaCommand> ExterniVazby { get; set; } = new();

    public List<SaveRecordHarmonogramValueCommand> HarmonogramHodnoty { get; set; } = new();

    public string? EditorTab { get; set; }

    public string? Presentation { get; set; }

    public string? ReturnUrl { get; set; }

    public int? JednaniIdProCislo { get; set; }

    public string? UiContext { get; set; }

    public int? MeetingId { get; set; }

    public string? ScheduleVersion { get; set; }
}

public sealed class DeleteRecordCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    public bool PotvrditSmazani { get; set; }
}

public sealed class SaveRecordExterniVazbaCommand
{
    public int Id { get; set; }
    public string? Typ { get; set; }
    public string? Cislo { get; set; }
    public string? PredpokladanaCena { get; set; }
    public string? Vyzva { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
}

public sealed class SaveRecordHarmonogramValueCommand
{
    public int TypId { get; set; }
    public int Hodnota { get; set; }
}

public sealed class AddCommentCommand
{
    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public int JednaniId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class UpdateCommentCommand
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class DeleteCommentCommand
{
    [Required]
    public int Id { get; set; }
}
