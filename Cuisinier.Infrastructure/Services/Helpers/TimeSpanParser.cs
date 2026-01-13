namespace Cuisinier.Infrastructure.Services.Helpers;

public class TimeSpanParser
{
    public TimeSpan? Parse(string? timeSpanString)
    {
        if (string.IsNullOrWhiteSpace(timeSpanString))
            return null;

        // Expected format: "HH:mm:ss" or "HH:mm"
        if (TimeSpan.TryParse(timeSpanString, out var result))
            return result;

        // Try to manually parse "HH:mm:ss" format
        var parts = timeSpanString.Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
        {
            var seconds = parts.Length > 2 && int.TryParse(parts[2], out var s) ? s : 0;
            return new TimeSpan(hours, minutes, seconds);
        }

        return null;
    }
}
