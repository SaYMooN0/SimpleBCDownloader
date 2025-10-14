namespace MBCDownloader.json;

public sealed class AlbumDto
{
    public string? AlbumId { get; init; }
    public string? AlbumUrl { get; init; }
    public string? Title { get; init; }
    public string? ArtistName { get; init; }
    public string? ArtistUrl { get; init; }
    public DateTimeOffset? DatePublished { get; init; }
    public string? CoverImageUrl { get; init; }
    public int? NumTracks { get; init; }
    public List<TrackDto> Tracks { get; init; } = new();
}