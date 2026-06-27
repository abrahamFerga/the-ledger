namespace TheLedger.Application.Ingestion.QuickAdd;

/// <summary>
/// Resolves relative Spanish date phrases ("hoy", "ayer", "antier", "el lunes") to a concrete
/// <see cref="DateOnly"/> anchored to <i>today</i> in <c>America/Mexico_City</c> (ADR-0011). The timezone is
/// pinned explicitly so the calendar day never depends on the server's local time. "Now" comes from an
/// injected <see cref="TimeProvider"/> so tests are deterministic.
/// </summary>
public static class MexicoCityClock
{
    /// <summary>IANA id; .NET 10 resolves it cross-platform. Windows fallback kept for older ICU data.</summary>
    public static readonly TimeZoneInfo TimeZone = ResolveMexicoCity();

    private static TimeZoneInfo ResolveMexicoCity()
    {
        foreach (var id in (string[])["America/Mexico_City", "Central Standard Time (Mexico)"])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // try the next id
            }
            catch (InvalidTimeZoneException)
            {
                // try the next id
            }
        }

        throw new InvalidOperationException("America/Mexico_City timezone is not available on this system.");
    }

    /// <summary>Today's calendar date in Mexico City, derived from the injected clock.</summary>
    public static DateOnly Today(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var nowInMexico = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZone);
        return DateOnly.FromDateTime(nowInMexico.DateTime);
    }

    /// <summary>
    /// Resolves a relative Spanish date phrase against <paramref name="today"/>. Unknown or absent phrasing
    /// returns <paramref name="today"/>. Day-of-week names resolve to the most recent past occurrence
    /// (today excluded), matching how "el lunes" reads in everyday Mexican Spanish.
    /// </summary>
    public static DateOnly ResolveRelative(string? phrase, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return today;
        }

        var normalized = Normalize(phrase);

        return normalized switch
        {
            var p when p.Contains("antier") || p.Contains("anteayer") => today.AddDays(-2),
            var p when p.Contains("ayer") => today.AddDays(-1),
            var p when p.Contains("manana") => today.AddDays(1), // "mañana" → tomorrow
            var p when p.Contains("hoy") => today,
            _ => ResolveWeekday(normalized, today),
        };
    }

    private static DateOnly ResolveWeekday(string normalized, DateOnly today)
    {
        foreach (var (name, dow) in Weekdays)
        {
            if (normalized.Contains(name))
            {
                return MostRecentPast(today, dow);
            }
        }

        return today;
    }

    /// <summary>Most recent past occurrence of <paramref name="target"/> (excluding today).</summary>
    private static DateOnly MostRecentPast(DateOnly today, DayOfWeek target)
    {
        var delta = ((int)today.DayOfWeek - (int)target + 7) % 7;
        delta = delta == 0 ? 7 : delta;
        return today.AddDays(-delta);
    }

    /// <summary>Lowercases and strips accents so "miércoles"/"miercoles"/"sábado" all match.</summary>
    private static string Normalize(string input)
    {
        var lowered = input.ToLowerInvariant();
        var builder = new System.Text.StringBuilder(lowered.Length);
        foreach (var c in lowered.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static readonly (string Name, DayOfWeek Day)[] Weekdays =
    [
        ("lunes", DayOfWeek.Monday),
        ("martes", DayOfWeek.Tuesday),
        ("miercoles", DayOfWeek.Wednesday),
        ("jueves", DayOfWeek.Thursday),
        ("viernes", DayOfWeek.Friday),
        ("sabado", DayOfWeek.Saturday),
        ("domingo", DayOfWeek.Sunday),
    ];
}
