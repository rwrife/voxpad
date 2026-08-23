namespace Voxpad.Desktop.ViewModels;

public sealed class TranscriptSegmentViewModel : ViewModelBase
{
    private readonly Action<string> textChanged;
    private string text;

    public TranscriptSegmentViewModel(
        int index,
        string text,
        long startMs,
        long endMs,
        Action<string> textChanged)
    {
        Index = index;
        this.text = text ?? string.Empty;
        StartMs = startMs;
        EndMs = endMs;
        this.textChanged = textChanged ?? throw new ArgumentNullException(nameof(textChanged));
    }

    public int Index { get; }

    public long StartMs { get; }

    public long EndMs { get; }

    public string StartLabel => FormatTimestamp(StartMs);

    public string EndLabel => FormatTimestamp(EndMs);

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value ?? string.Empty))
            {
                textChanged(text);
            }
        }
    }

    private static string FormatTimestamp(long milliseconds)
    {
        var timestamp = TimeSpan.FromMilliseconds(milliseconds);
        return $"{(int)timestamp.TotalHours:00}:{timestamp.Minutes:00}:{timestamp.Seconds:00}.{timestamp.Milliseconds:000}";
    }
}
