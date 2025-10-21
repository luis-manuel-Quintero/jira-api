public class AttachmentDto
{
    public string FileName { get; set; } = "";
    public string ContentUrl { get; set; } = "";
    public string? DownloadLocalPath { get; set; }

    public async Task<byte[]> DownloadBytesAsync(HttpClient httpClient)
        => await httpClient.GetByteArrayAsync(ContentUrl);
}