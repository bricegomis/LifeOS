namespace LifeOS.Application.Common.Files;

/// <summary>A file written by the storage: server-generated name, checked type, SHA-256 of the bytes.</summary>
public sealed record StoredFile(string FileName, string ContentType, string Sha256);
