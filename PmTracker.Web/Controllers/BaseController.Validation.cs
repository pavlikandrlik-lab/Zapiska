using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    protected Dictionary<string, string[]> BuildModelStateFieldErrors()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ModelState)
        {
            if (entry.Value is null || entry.Value.ValidationState == ModelValidationState.Valid)
            {
                continue;
            }

            var key = NormalizeModelStateKey(entry.Key);
            var messages = entry.Value.Errors
                .Select(error =>
                    !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage.Trim()
                        : error.Exception?.Message?.Trim())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Cast<string>()
                .ToList();

            if (messages.Count == 0)
            {
                var attemptedValue = entry.Value.AttemptedValue;
                if (string.IsNullOrWhiteSpace(attemptedValue) && entry.Value.RawValue is string[] rawArray)
                {
                    attemptedValue = string.Join(", ", rawArray.Where(value => !string.IsNullOrWhiteSpace(value)));
                }
                else if (string.IsNullOrWhiteSpace(attemptedValue) && entry.Value.RawValue is not null)
                {
                    attemptedValue = entry.Value.RawValue.ToString();
                }

                messages.Add(!string.IsNullOrWhiteSpace(attemptedValue)
                    ? $"Neplatná hodnota ({key}): \"{attemptedValue}\"."
                    : $"Neplatná hodnota ({key}).");
            }

            if (!result.TryGetValue(key, out var existing))
            {
                result[key] = messages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                continue;
            }

            existing.AddRange(messages);
            result[key] = existing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return result.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    protected static string JoinFieldErrors(Dictionary<string, string[]> fieldErrors, string fallback)
    {
        var messages = fieldErrors.Values
            .SelectMany(values => values)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return messages.Count == 0 ? fallback : string.Join(" | ", messages);
    }

    private static string NormalizeModelStateKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return string.Empty;
        }

        var key = rawKey.Trim();
        var dotIndex = key.IndexOf('.');
        if (dotIndex <= 0)
        {
            return key;
        }

        var prefix = key[..dotIndex];
        if (prefix.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            return key[(dotIndex + 1)..];
        }

        if (prefix.EndsWith("command", StringComparison.OrdinalIgnoreCase))
        {
            return key[(dotIndex + 1)..];
        }

        return key;
    }
}
