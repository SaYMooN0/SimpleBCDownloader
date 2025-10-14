using System.Diagnostics;
using System.Text;
using HtmlAgilityPack;
using MBCDownloader.json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SimpleBCDownloader;

class Program
{
    static async Task Main(string[] args) {
        // "https://vbeo.bandcamp.com/album/--17"

        CancellationToken ct = new CancellationToken();
        if (args.Length == 0) {
            AppLogger.Error("No album link provided");
            return;
        }

        string albumLink = args[0]?.Trim() ?? string.Empty;
        if (
            string.IsNullOrWhiteSpace(albumLink)
            || !albumLink.StartsWith("http")
            || !albumLink.Contains(".bandcamp.com/album")
        ) {
            AppLogger.Error(
                "Invalid album link provided. Album link must be first argument and lead to an album. For example \"https://vbeo.bandcamp.com/album/--17\""
            );
            return;
        }

        AppLogger.IntermediateSuccess("Download started. Album link: {1}", albumLink);


        await DownloadAlbumAsync(albumLink, ct);
        AppLogger.Success("Download finished");
    }

    private static async Task DownloadAlbumAsync(string url, CancellationToken ct) {
        const int msBudget = 8000;

        string html = await DumpDomViaEdgeAsync(url, msBudget, ct);
        AppLogger.IntermediateSuccess("Downloaded album page html");

        HtmlDocument htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        AppLogger.IntermediateSuccess("Parsed page into {1} type", typeof(HtmlDocument));


        AlbumDto album = ExtractAlbumData(htmlDoc);
        AppLogger.IntermediateSuccess("Album data fetched. Expected tracks count: {1}", album.Tracks.Count());
        Dictionary<long, string> idToPath = ExtractTrackFiles(htmlDoc);

        if (album.Tracks.Count != idToPath.Count) {
            AppLogger.Warning(
                "Track count mismatch. Count in album data: {1}, in files list: {2}",
                album.Tracks.Count, idToPath.Count
            );
        }


        await SaveAlbum(album, idToPath, ct);
    }

    private async static Task SaveAlbum(AlbumDto album, Dictionary<long, string> idToPath, CancellationToken ct) { }

    public static async Task<string> DumpDomViaEdgeAsync(string url, int msBudget, CancellationToken ct) {
        string? edgePath = GetEdgePathByConvention();
        if (edgePath is null) {
            AppLogger.Error("Edge executable not found");
            Environment.Exit(1);
            return null;
        }

        string dumpDir = Path.Combine(Path.GetTempPath(), "edge-headless-" + Guid.NewGuid());
        Directory.CreateDirectory(dumpDir);

        string args =
            $" --headless=new " +
            $"--disable-gpu" +
            $" --user-data-dir=\"{dumpDir}\"" +
            $" --virtual-time-budget={msBudget}" +
            $" --dump-dom \"{url}\"";

        ProcessStartInfo psi = new ProcessStartInfo {
            FileName = edgePath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };


        using var p = Process.Start(psi)!;
        string stdout = await p.StandardOutput.ReadToEndAsync(ct);
        string stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);

        Directory.Delete(dumpDir, true);

        if (p.ExitCode != 0) {
            AppLogger.Error("Couldn't get DOM dump from Edge browser. Stderr: {1}", stderr);
            Environment.Exit(1);
        }

        return stdout;
    }

    private static string? GetEdgePathByConvention() {
        string[] candidates = [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static Dictionary<long, string> ExtractTrackFiles(HtmlDocument htmlDoc) {
        HtmlNode? pageData = htmlDoc.DocumentNode
            .SelectSingleNode("//script[@type='text/javascript' and @data-tralbum]");
        if (pageData is null) {
            AppLogger.Error("Could not find html node to extract track files");
            Environment.Exit(1);
        }

        string rawAttribute = pageData.GetAttributeValue("data-tralbum", null);
        if (string.IsNullOrEmpty(rawAttribute)) {
            AppLogger.Error("Attribute with expected track files value is empty");
            Environment.Exit(1);
        }

        string parsedAttribute = HtmlEntity.DeEntitize(rawAttribute);
        JObject jsonObj;
        try {
            jsonObj = JObject.Parse(parsedAttribute);
        }
        catch (Exception e) {
            AppLogger.Error("Couldn't deserialize attribute value with expected track files");
            Environment.Exit(1);
            return null;
        }

        Dictionary<long, string>? trackIdToPath = jsonObj["trackinfo"]?
            .OfType<JObject>()
            .Select(t => new {
                id = t.Value<long?>("track_id") ?? t.Value<long?>("id"),
                url = t["file"]?["mp3-128"]?.ToString()
            })
            .Where(x => x.id.HasValue && !string.IsNullOrEmpty(x.url))
            .ToDictionary(x => x.id!.Value, x => x.url!);

        if (trackIdToPath is null) {
            AppLogger.Error("Couldn't correctly extract track file paths from deserialized JSON");
            Environment.Exit(1);
        }

        return trackIdToPath;
    }

    private static AlbumDto ExtractAlbumData(HtmlDocument htmlDoc) {
        HtmlNode? nodeWithAlbumData = htmlDoc.DocumentNode.SelectSingleNode("//script[@type='application/ld+json']");

        if (nodeWithAlbumData is null) {
            AppLogger.Error("Could not find html node with html");
            Environment.Exit(1);
        }

        AppLogger.IntermediateSuccess("Successfully extracted html node with album data ");

        var rawAlbumDataText = nodeWithAlbumData.InnerText.Trim();
        if (string.IsNullOrEmpty(rawAlbumDataText)) {
            AppLogger.Error("Extracted album data is empty");
            Environment.Exit(1);
            return null;
        }

        string albumJsonText = HtmlEntity.DeEntitize(rawAlbumDataText);

        JsonSerializerSettings settings = new JsonSerializerSettings {
            Converters = { new AlbumDtoConverter() }
        };

        try {
            return JsonConvert.DeserializeObject<AlbumDto>(albumJsonText, settings)!;
        }
        catch (Exception e) {
            AppLogger.Error("Couldn't deserialize album into {1} type, ex: {2}", typeof(AlbumDto), e.ToString());
            Environment.Exit(1);
            return null;
        }
    }
}