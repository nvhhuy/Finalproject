namespace Infrastructure.Scheduling;

/// <summary>
///     Converts standard 5-field Unix cron (used by <see cref="Cronos.CronExpression" />) to the 6-field
///     format required by Quartz.NET (seconds first; <c>?</c> for day-of-week vs day-of-month).
/// </summary>
internal static class QuartzCronExpression
{
    /// <summary>
    ///     <paramref name="expression" /> may be 5-field (minute hour dom month dow) or already 6–7 field Quartz.
    /// </summary>
    public static string ToQuartz(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new FormatException("Cron expression cannot be empty.");

        var parts = expression.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            5 => ToSixField(parts),
            6 => NormalizeSixField(parts),
            7 => string.Join(' ', parts),
            _ => throw new FormatException(
                $"Unsupported cron field count ({parts.Length}). Expected 5 (Unix) or 6–7 (Quartz). Expression: '{expression}'.")
        };
    }

    private static string ToSixField(string[] five)
    {
        var minute = five[0];
        var hour = five[1];
        var dom = five[2];
        var month = five[3];
        var dow = five[4];
        ResolveDomAndDow(ref dom, ref dow);
        return $"0 {minute} {hour} {dom} {month} {dow}";
    }

    private static string NormalizeSixField(string[] six)
    {
        var dom = six[3];
        var dow = six[5];
        ResolveDomAndDow(ref dom, ref dow);
        return $"{six[0]} {six[1]} {six[2]} {dom} {six[4]} {dow}";
    }

    /// <summary>Quartz forbids <c>*</c> on both day-of-month and day-of-week.</summary>
    private static void ResolveDomAndDow(ref string dom, ref string dow)
    {
        if (dom == "*" && dow == "*")
            dow = "?";
        else if (dom == "*" && dow != "?" && dow != "*")
            dom = "?";
        else if (dow == "*" && dom != "?" && dom != "*")
            dow = "?";
    }
}