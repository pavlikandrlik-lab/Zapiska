using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Review fix M1 (2026-10-12): textarea „Důvod změny" má <c>maxlength="1000"</c>, prohlížeč
/// počítá každé zalomení řádku jako 1 znak (textarea.value používá \n), ale multipart form-data
/// (ajax.js <c>new FormData(form)</c>) odesílá zalomení jako CRLF — server tak u hodnoty na
/// hranici limitu vidí víc znaků, než kolik jich prohlížeč povolil, a [StringLength(1000)]
/// validaci zamítne hodnotu, kterou uživatel mohl vepsat. Setter proto normalizuje \r\n i
/// osamocené \r na \n, takže validace i uložená hodnota vidí stejnou délku jako prohlížeč.
/// </summary>
public sealed class SaveRecordCommandDuvodZmenyTests
{
    private static SaveRecordCommand ValidCommand(string? duvodZmeny) => new()
    {
        ProjektId = 1,
        Kategorie = "U",
        Stav = "OPEN",
        Nazev = "Test",
        VlastnikId = 1,
        DatumZalozeni = new DateTime(2026, 1, 1),
        TerminUkonceni = new DateTime(2026, 12, 31),
        Subsystem = "SYS",
        DuvodZmeny = duvodZmeny
    };

    private static bool TryValidate(SaveRecordCommand command, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        var context = new ValidationContext(command);
        return Validator.TryValidateObject(command, context, results, validateAllProperties: true);
    }

    /// <summary>Řetězec o <paramref name="textLength"/> znacích textu rozdělený do 5 řádků CRLF.</summary>
    private static string BuildCrlfReason(int textLength)
    {
        var perLine = textLength / 5;
        var last = textLength - perLine * 4;
        var text = new string('a', textLength);
        return string.Join("\r\n",
            text.Substring(0, perLine),
            text.Substring(perLine, perLine),
            text.Substring(perLine * 2, perLine),
            text.Substring(perLine * 3, perLine),
            text.Substring(perLine * 4, last));
    }

    [Fact]
    public void DuvodZmeny_SetterNormalizujeCrlfNaLf()
    {
        var command = ValidCommand("Řádek 1\r\nŘádek 2\r\nŘádek 3");
        command.DuvodZmeny.Should().Be("Řádek 1\nŘádek 2\nŘádek 3");
        command.DuvodZmeny.Should().NotContain("\r");
    }

    [Fact]
    public void DuvodZmeny_SetterNormalizujeOsamoceneCr()
    {
        var command = ValidCommand("Řádek 1\rŘádek 2");
        command.DuvodZmeny.Should().Be("Řádek 1\nŘádek 2");
    }

    [Fact]
    public void DuvodZmeny_1000ZnakuJakoVProhlizeci_SCrlfProjdeValidaci()
    {
        // Prohlížeč: 996 znaků textu + 4 zalomení (\n) = 1000 znaků v textarea.value
        // (přesně na hranici maxlength="1000"). Multipart form-data je odešle jako CRLF, takže
        // syrová hodnota z požadavku má 996 + 4*2 = 1004 znaků.
        var raw = BuildCrlfReason(996);
        raw.Should().HaveLength(1004);

        var command = ValidCommand(raw);
        command.DuvodZmeny.Should().HaveLength(1000);
        command.DuvodZmeny.Should().NotContain("\r");

        TryValidate(command, out var results).Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void DuvodZmeny_1001ZnakuJakoVProhlizeci_NeprojdeValidaci()
    {
        // O jeden znak textu víc než v předchozím testu → 1001 znaků, jak by je počítal prohlížeč
        // (textarea maxlength by takovou hodnotu už nedovolila zadat, ale ověřujeme, že normalizace
        // sama o sobě limit neobchází).
        var raw = BuildCrlfReason(997);
        var command = ValidCommand(raw);
        command.DuvodZmeny.Should().HaveLength(1001);

        TryValidate(command, out var results).Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains("DuvodZmeny"));
    }
}
