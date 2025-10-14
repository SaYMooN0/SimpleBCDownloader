using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MBCDownloader.json;

public sealed class AlbumDtoConverter : JsonConverter<AlbumDto>
{
    public override AlbumDto ReadJson(JsonReader reader, Type objectType, AlbumDto? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        var root = JObject.Load(reader);

        var albumId = (string?)root.SelectToken("@id");
        var albumUrl = (string?)root.SelectToken("mainEntityOfPage") ?? albumId;
        var title = (string?)root.SelectToken("name");
        var artist = (string?)root.SelectToken("byArtist.name");
        var artistUrl = (string?)root.SelectToken("byArtist.@id");

        var datePub = ParseGmt((string?)root.SelectToken("datePublished"));
        string? cover =
            (string?)root.SelectToken("image") ??
            (string?)root.SelectToken("albumRelease[0].image[0]");

        List<TrackDto> tracks = [];
        var items = root.SelectToken("track.itemListElement") as JArray;
        if (items != null) {
            foreach (var li in items.OfType<JObject>()) {
                var item = li["item"] as JObject;
                if (item is null) continue;

                var pos = (int?)li.SelectToken("position");
                var tTitle = (string?)item.SelectToken("name");
                var tUrl = (string?)item.SelectToken("mainEntityOfPage") ?? (string?)item.SelectToken("@id");
                var tDurRaw = (string?)item.SelectToken("duration");
                TimeSpan? tDur = TryParseIsoDuration(tDurRaw);

                long? trackId = null;
                var addProps = item.SelectToken("additionalProperty") as JArray;
                if (addProps != null) {
                    foreach (var ap in addProps.OfType<JObject>()) {
                        if ((string?)ap["name"] == "track_id") {
                            var val = ap["value"];
                            if (val != null) {
                                if (val.Type == JTokenType.Integer) trackId = (long?)val;
                                else if (long.TryParse(val.ToString(), out var l)) trackId = l;
                            }

                            break;
                        }
                    }
                }

                var lyrics = (string?)item.SelectToken("recordingOf.lyrics.text");

                tracks.Add(new TrackDto {
                    Position = pos,
                    Title = tTitle,
                    Url = tUrl,
                    Duration = tDur,
                    TrackId = trackId,
                    Lyrics = lyrics?.Trim()
                });
            }
        }

        var numTracks = (int?)root.SelectToken("numTracks") ?? tracks.Count;

        return new AlbumDto {
            AlbumId = albumId,
            AlbumUrl = albumUrl,
            Title = title,
            ArtistName = artist,
            ArtistUrl = artistUrl,
            DatePublished = datePub,
            CoverImageUrl = cover,
            NumTracks = numTracks,
            Tracks = tracks.OrderBy(t => t.Position ?? int.MaxValue).ThenBy(t => t.Title).ToList()
        };
    }

    public override void WriteJson(JsonWriter writer, AlbumDto? value, JsonSerializer serializer)
        => throw new NotImplementedException();

    private static DateTimeOffset? ParseGmt(string? s) {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var formats = new[] { "dd MMM yyyy HH':'mm':'ss 'GMT'", "d MMM yyyy HH':'mm':'ss 'GMT'" };
        if (DateTimeOffset.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;

        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dto)) {
            return dto.ToUniversalTime();
        }

        return null;
    }

    private static TimeSpan? TryParseIsoDuration(string? iso) {
        if (string.IsNullOrWhiteSpace(iso)) {
            return null;
        }

        try {
            return System.Xml.XmlConvert.ToTimeSpan(iso);
        }
        catch {
            try {
                return System.Xml.XmlConvert.ToTimeSpan(
                    iso.StartsWith("P", StringComparison.OrdinalIgnoreCase) ? iso : "P" + iso);
            }
            catch {
                return null;
            }
        }
    }
}