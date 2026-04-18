namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Jedna položka pro pm-select.
/// </summary>
/// <param name="Value">Hodnota odesílaná ve formuláři (atribut value).</param>
/// <param name="Text">Zobrazovaný text (obsah option).</param>
/// <param name="selected">Je defaultně vybraná.</param>
/// <param name="disabled">Zakázaná volba.</param>
public sealed record PmSelectOption(string Value, string Text, bool selected, bool disabled);
