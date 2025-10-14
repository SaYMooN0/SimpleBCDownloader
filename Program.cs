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
        AppLogger.Error("Not implemented yet");
    }
}