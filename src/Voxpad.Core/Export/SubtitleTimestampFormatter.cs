namespace Voxpad.Core.Export;

internal static class SubtitleTimestampFormatter
{
    public static string ToSrtTimestamp(long milliseconds)
    {
        return Format(milliseconds, ',');
    }

    public static string ToVttTimestamp(long milliseconds)
    {
        return Format(milliseconds, '.');
    }

    private static string Format(long milliseconds, char millisecondSeparator)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timestamp must be non-negative.");
        }

        var hours = milliseconds / 3_600_000;
        var minutes = (milliseconds / 60_000) % 60;
        var seconds = (milliseconds / 1_000) % 60;
        var millis = milliseconds % 1_000;

        return $"{hours:00}:{minutes:00}:{seconds:00}{millisecondSeparator}{millis:000}";
    }
}
