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

    /// <summary>
    /// Spec 2026-10-13 §4.5 — k čemu platí změna stavu: prázdné = dnes, jinak id jednání.
    /// Server ho použije jen při skutečné změně stavu.
    /// </summary>
    public string? StavPlatiK { get; set; }

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
    /// Plán D (schéma 3 — CREATE_RECORD): pre-bound vazby mezi bublinami
    /// HOT_VYJADRENI a kroky harmonogramu nového záznamu. Aplikují se až při
    /// schválení návrhu, kdy teprve vznikne cílový záznam.
    /// </summary>
    public List<HarmonogramVazbaDto> HarmonogramVazby { get; set; } = new();

    /// <summary>
    /// Spec 2026-09-17 §5.2 — verze záznamu z doby načtení editoru (id posledního
    /// auditního zápisu). Prázdné = nový záznam nebo formulář z doby před nasazením
    /// → kontrola se přeskočí.
    /// </summary>
    public string? RecordVersion { get; set; }

    public string? EditorTab { get; set; }

    public string? ReturnUrl { get; set; }

    public int? JednaniIdProCislo { get; set; }

    public string? UiContext { get; set; }

    public int? MeetingId { get; set; }


    /// <summary>
    /// FIX 2026-05-04: master switch "Automatické vyplňování harmonogramu" (Auto/Manual).
    /// Hodnota přijatá z form pole <c>HarmonogramRezim</c> kontroluje SkutecnostRezim
    /// pro auto-eligible krok řádky (kroky 1/3/4/6/7/10). V ručním režimu jdou změny skutečnosti
    /// kroků 1/3/4/6/7/10 v HarmonogramHodnoty; v automatickém se ignorují a přepnutí na
    /// automatiku smaže jejich data, která nedodal automat (spec 2026-10-14 R2).
    /// </summary>
    public string? HarmonogramRezim { get; set; }

    public const int DuvodZmenyMaxLength = 1000;

    private string? _duvodZmeny;

    /// <summary>
    /// Důvod změny z návrhu změny termínu a harmonogramu (uživatel 2026-10-08) — nepovinné sdělení
    /// autora schvalovateli; po schválení se uloží do zaznam_historie_terminu.duvod. Přímá úprava
    /// záznamu ho nepoužívá.
    /// </summary>
    /// <remarks>
    /// Review fix M1 (2026-10-12): textarea má <c>maxlength="1000"</c> a prohlížeč počítá každé
    /// zalomení jako 1 znak (textarea.value používá \n), ale multipart form-data odesílá \r\n.
    /// Setter normalizuje \r\n i osamocené \r na \n hned při model bindingu, takže
    /// [StringLength] i uložená hodnota vidí stejnou délku, jakou povolil prohlížeč.
    /// </remarks>
    [StringLength(DuvodZmenyMaxLength)]
    public string? DuvodZmeny
    {
        get => _duvodZmeny;
        set => _duvodZmeny = value?.Replace("\r\n", "\n").Replace("\r", "\n");
    }
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
    public int? VyzvaId { get; set; }
    public bool ZaradidDoVyzvy { get; set; }

    /// <summary>
    /// Text požadavku do výzvy (spec 2026-09-08). Rich text HTML z Quillu; před uložením
    /// projde sanitizací. Renderuje se jen u PNF, u ostatních typů zůstane null.
    /// </summary>
    public string? Pozadavek { get; set; }

    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
}

public sealed class SaveRecordHarmonogramValueCommand
{
    public int Poradi { get; set; }
    public DateTime? PlanDatum { get; set; }

    /// <summary>
    /// Spec 2026-10-14 — skutečnost kroku, jak ji uživatel nechal ve formuláři (prázdné = smazáno
    /// nebo nevyplněno). V payloadu návrhu (ActualHarmonogramHodnoty) jde jen u změněných kroků.
    /// </summary>
    public DateTime? SkutecnostDatum { get; set; }

    /// <summary>
    /// Spec 2026-10-14 R6 — skutečnost, se kterou se editor vykreslil. Server zapíše jen krok,
    /// kde se <see cref="SkutecnostDatum"/> od této hodnoty liší, takže nedotčený krok si nechá
    /// hodnotu i zdroj a novější hodnotu automatu z doby editace nepřepíše stará.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? SkutecnostDatumPuvodni { get; set; }
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
