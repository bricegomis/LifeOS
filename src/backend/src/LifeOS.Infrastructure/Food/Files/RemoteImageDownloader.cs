using System.Net;
using System.Net.Sockets;

namespace LifeOS.Infrastructure.Food.Files;

/// <summary>An image fetched from the web: its bytes and the type read from its signature.</summary>
public sealed record DownloadedImage(byte[] Bytes, string ContentType);

/// <summary>Fetches an image the assistant found on the web (ADR 0011, § 4).</summary>
public interface IRemoteImageSource
{
    Task<DownloadedImage> DownloadAsync(string url, CancellationToken ct);
}

/// <summary>
/// Downloads an image by URL for the assistant (ADR 0011, § 4): <c>https</c> only, every
/// connection (redirects included) refused unless all the host's addresses are public, a
/// timeout and a size limit. The type is read from the file signature, never trusted from the
/// server; the storage then applies the same checks as an upload from the app.
/// </summary>
public sealed class RemoteImageDownloader : IRemoteImageSource, IDisposable
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private readonly HttpClient _http;

    public RemoteImageDownloader()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = ConnectToPublicAddressAsync,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LifeOS/1.0 (+image import)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg, image/png, image/webp;q=0.9, */*;q=0.1");
    }

    public async Task<DownloadedImage> DownloadAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("L'adresse de l'image doit être une URL https.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("L'adresse de l'image ne doit pas contenir d'identifiants.");

        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new ArgumentException($"Téléchargement de l'image refusé par le site ({(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength > MaxBytes)
                throw new ArgumentException("L'image dépasse la limite de 10 Mo.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxBytes) throw new ArgumentException("L'image dépasse la limite de 10 Mo.");
                buffer.Write(chunk, 0, read);
            }
            var bytes = buffer.ToArray();
            return new DownloadedImage(bytes, DetectContentType(bytes)
                ?? throw new ArgumentException("Le fichier téléchargé n'est pas une image JPEG, PNG ou WebP."));
        }
        catch (HttpRequestException exception) when (exception.InnerException is BlockedAddressException blocked)
        {
            throw new ArgumentException(blocked.Message);
        }
        catch (HttpRequestException)
        {
            throw new ArgumentException("Impossible de télécharger l'image à cette adresse.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ArgumentException("Le téléchargement de l'image a pris trop de temps.");
        }
    }

    /// <summary>JPEG, PNG or WebP from the first bytes; null for anything else.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? "image/jpeg"
        : bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
        : bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? "image/webp"
        : null;

    /// <summary>
    /// False for loopback, private, link-local, carrier-grade NAT, multicast and reserved
    /// addresses, so a URL can never reach the server itself or its network.
    /// </summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19)));
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || address.IsIPv6UniqueLocal || address.IsIPv6Teredo
                // 64:ff9b::/96 (NAT64) and 2002::/16 (6to4) embed an IPv4 address that may be private.
                || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b)
                || (b[0] == 0x20 && b[1] == 0x02));
        }
        return false;
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        if (addresses.Length == 0 || !addresses.All(IsPublicAddress))
            throw new BlockedAddressException("Cette adresse d'image n'est pas autorisée (réseau privé ou local).");
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class BlockedAddressException(string message) : Exception(message);
}
