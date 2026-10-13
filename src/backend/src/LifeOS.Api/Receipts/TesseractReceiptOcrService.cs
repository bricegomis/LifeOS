using System.Diagnostics;
using LifeOS.Application.Receipts;

namespace LifeOS.Api.Receipts;

public sealed class TesseractReceiptOcrService(
    IConfiguration configuration,
    ILogger<TesseractReceiptOcrService> logger) : IReceiptOcrService
{
    public async Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        var executable = configuration["Receipts:TesseractExecutable"] ?? "tesseract";
        var language = configuration["Receipts:TesseractLanguage"] ?? "fra+eng";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add(imagePath);
        process.StartInfo.ArgumentList.Add("stdout");
        process.StartInfo.ArgumentList.Add("-l");
        process.StartInfo.ArgumentList.Add(language);
        process.StartInfo.ArgumentList.Add("--psm");
        process.StartInfo.ArgumentList.Add("6");

        try
        {
            if (!process.Start()) throw new IOException("Impossible de démarrer Tesseract.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            logger.LogWarning(exception, "Tesseract is unavailable for receipt OCR");
            throw new IOException("Le moteur OCR Tesseract est indisponible. Vérifiez son installation et le volume tessdata français.", exception);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new IOException("Le traitement OCR a dépassé 45 secondes.");
        }

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(error) ? "" : $" {error.Trim()[..Math.Min(error.Trim().Length, 400)]}";
            logger.LogWarning("Tesseract exited with code {ExitCode}: {Error}", process.ExitCode, error);
            throw new IOException($"Tesseract a échoué (code {process.ExitCode}).{detail}");
        }

        if (output.Length > 100_000) throw new IOException("Le texte OCR dépasse la limite de conservation de 100 Ko.");
        return output;
    }
}
