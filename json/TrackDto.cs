namespace MBCDownloader.json;

public sealed class TrackDto
{
    public int? Position { get; init; }
    public string? Title { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? Url { get; init; }
    public long? TrackId { get; init; }
    public string? Lyrics { get; init; }
}