using System.Text.Json;
using BootstrapperShared;
using dboot.SubSystem;
using Serilog;

namespace dboot.Core.Http;

/// <summary>
/// A client for checking updates and fetching catalogs from a specified base URL.
/// </summary>
public class UpdateClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler _httpClientHandler;
    private readonly string _baseUrl;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateClient"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL for the update server.</param>
    public UpdateClient(string baseUrl)
    {
        _baseUrl = baseUrl;

        _httpClientHandler = new HttpClientHandler
        {
            // Set any specific handler properties if needed
        };

        _httpClient = new HttpClient(_httpClientHandler)
        {
            BaseAddress = new Uri(_baseUrl)
        };

        // Set default User-Agent header
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("UpdateClient/1.0");
    }

    /// <summary>
    /// Fetches the catalog from the update server and deserializes it into a <see cref="Catalog"/> object.
    /// </summary>
    /// <returns>The <see cref="Catalog"/> object if successful; otherwise, null.</returns>
    public async Task<Catalog?> GetCatalog(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("catalog.json", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<Catalog>(jsonString, SourceGenerationContext.Default.Catalog);
            }
            else
            {
                Log.Error("Failed to fetch catalog. Status Code: {StatusCode}, Reason: {ReasonPhrase}",
                          response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching or deserializing catalog");
            return null;
        }
    }

    public async Task<bool> DownloadZipFile(string relativePath, string destinationFilePath, ProgressDialog progressDialog, CancellationToken cancellationToken)
    {
        try
        {
            // Set the dialog to non-marquee mode
            progressDialog.Marquee = false;
            progressDialog.Maximum = 100; // Set maximum to 100 for percentage

            // Send a HEAD request to get the file size
            var headResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, relativePath), cancellationToken);
            if (!headResponse.IsSuccessStatusCode || !headResponse.Content.Headers.ContentLength.HasValue)
            {
                Log.Error("Failed to retrieve file size. Status Code: {StatusCode}, Reason: {ReasonPhrase}",
                          headResponse.StatusCode, headResponse.ReasonPhrase);
                return false;
            }
            long totalBytes = headResponse.Content.Headers.ContentLength.Value;

            // Start downloading the file in chunks
            var response = await _httpClient.GetAsync(relativePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Failed to download ZIP file. Status Code: {StatusCode}, Reason: {ReasonPhrase}",
                          response.StatusCode, response.ReasonPhrase);
                return false;
            }

            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            byte[] buffer = new byte[8192]; // 8 KB buffer
            long totalRead = 0;
            int bytesRead;
            int lastReportedPercentage = 0;

            while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                // Calculate percentage
                int currentPercentage = (int)(totalRead * 100 / totalBytes);

                // Update progress if the percentage has changed
                if (currentPercentage > lastReportedPercentage)
                {
                    progressDialog.Value = currentPercentage;
                    lastReportedPercentage = currentPercentage;

                    // Update dialog line 3 with bytes read / total in KB
                    string progressText = $"{totalRead / 1024:N0} KB / {totalBytes / 1024:N0} KB";
                    progressDialog.Line3 = progressText;
                }
            }

            Log.Information("Successfully downloaded ZIP file from {RelativePath} to {DestinationFilePath}", relativePath, destinationFilePath);
            return true;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Download was canceled by the user.");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error downloading ZIP file from {RelativePath}", relativePath);
            return false;
        }
    }


    /// <summary>
    /// Disposes the <see cref="UpdateClient"/> and its resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
            _disposed = true;
        }
    }
}
