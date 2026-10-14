namespace LifeOS.Application.Receipts;

public interface IReceiptOcrService
{
    Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken);
}
