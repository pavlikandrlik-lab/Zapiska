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

    /// <summary>
    /// Plán D: ruční skutečnost pro kroky 2/5/8/9. Mimo návrhový workflow
    /// (přímá editace, schéma 1) má tato kolekce prioritu nad
    /// <see cref="HarmonogramHodnoty"/>, protože je vyjádřená absolutním
    /// kalendářním datem, které server převádí na odchylku vůči plánu.
    /// </summary>
    public List<ManualActualKrokDto> ManualActualKroky { get; set; } = new();

    /// <summary>
    /// Plán D (schéma 3 — CREATE_RECORD): pre-bound vazby mezi bublinami
    /// HOT_VYJADRENI a kroky harmonogramu nového záznamu. Aplikují se až při
    /// schválení návrhu, kdy teprve vznikne cílový záznam.
    /// </summary>
    public List<HarmonogramVazbaDto> HarmonogramVazby { get; set; } = new();

    public string? EditorTab { get; set; }

    public string? ReturnUrl { get; set; }

    public int? JednaniIdProCislo { get; set; }

    public string? UiContext { get; set; }

    public int? MeetingId { get; set; }

    public string? ScheduleVersion { get; set; }

    /// <summary>
    /// FIX 2026-05-04: master switch "Automatické vyplňování harmonogramu" (Auto/Manual).
    /// Hodnota přijatá z form pole <c>HarmonogramRezim</c> kontroluje SkutecnostRezim
    /// pro auto-eligible HS0X_DELAY rows (kroky 1/3/4/6/7/10). V Manual rezimu navíc
    /// <see cref="ManualActualKroky"/> může obsahovat ruční datumy i pro tyto kroky.
    /// V Auto rezimu se serverside spustí sync (re-fill ze ServiceDesk vyjádření) a
    /// jakékoli odeslané manual datumy pro auto-eligible kroky se ignorují (klient
    /// je nepošle, ale server defensivně filtruje).
    /// </summary>
    public string? HarmonogramRezim { get; set; }
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
    public int? VyzvaId { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
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
