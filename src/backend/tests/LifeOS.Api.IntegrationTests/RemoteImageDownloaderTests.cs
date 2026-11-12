using System.Net;
using LifeOS.Infrastructure.Food.Files;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0011, § 4: the assistant's image URLs never reach the server or its network. No database needed.</summary>
public sealed class RemoteImageDownloaderTests
{
    [Theory]
    [InlineData("http://example.com/carotte.jpg")]
    [InlineData("ftp://example.com/carotte.jpg")]
    [InlineData("pas une url")]
    [InlineData("https://user:secret@example.com/carotte.jpg")]
    [InlineData("https://localhost/carotte.jpg")]
    [InlineData("https://127.0.0.1/carotte.jpg")]
    [InlineData("https://[::1]/carotte.jpg")]
    [InlineData("https://10.0.0.5/carotte.jpg")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://192.168.1.10/carotte.jpg")]
    public async Task Only_https_urls_to_public_addresses_are_downloaded(string url)
    {
        using var downloader = new RemoteImageDownloader();
        await Assert.ThrowsAsync<ArgumentException>(() => downloader.DownloadAsync(url, CancellationToken.None));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:192.168.0.1", false)]
    [InlineData("64:ff9b::a00:1", false)]
    public void Private_and_local_addresses_are_refused(string address, bool isPublic) =>
        Assert.Equal(isPublic, RemoteImageDownloader.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public void The_image_type_is_read_from_the_file_signature()
    {
        Assert.Equal("image/png", RemoteImageDownloader.DetectContentType([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0]));
        Assert.Equal("image/jpeg", RemoteImageDownloader.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal("image/webp", RemoteImageDownloader.DetectContentType("RIFF\0\0\0\0WEBP"u8));
        Assert.Null(RemoteImageDownloader.DetectContentType("<html>"u8));
    }
}
