namespace RomM.Client.Csdb;

/// <summary>Options for CSDb library ingest and search policy.</summary>
public sealed class CsdbLibraryOptions
{
    public required string LibraryRomsRoot { get; set; }

    public string? HvscRoot { get; set; }

    public string UserAgent { get; set; } = "RomM.Client.Csdb";

    public TimeSpan MinRequestInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    public int DefaultSearchLimit { get; set; } = 25;

    public int MaxSearchLimit { get; set; } = 50;

    public int MaxIngestBatch { get; set; } = 20;

    /// <summary>
    /// Ensures required paths are present and search-limit policy is within the CSDb ceiling.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LibraryRomsRoot))
        {
            throw new ArgumentException("LibraryRomsRoot is required.", nameof(LibraryRomsRoot));
        }

        if (MaxSearchLimit > 50)
        {
            throw new ArgumentException(
                "MaxSearchLimit must be less than or equal to 50.",
                nameof(MaxSearchLimit));
        }
    }

    /// <summary>
    /// Resolves a search limit: null uses <see cref="DefaultSearchLimit"/>; values above
    /// <see cref="MaxSearchLimit"/> raise <see cref="CsdbPolitenessException"/>.
    /// </summary>
    public int ClampSearchLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultSearchLimit;
        }

        if (limit.Value > MaxSearchLimit)
        {
            throw new CsdbPolitenessException(
                $"Search limit {limit.Value} exceeds MaxSearchLimit {MaxSearchLimit}.");
        }

        return limit.Value;
    }
}
