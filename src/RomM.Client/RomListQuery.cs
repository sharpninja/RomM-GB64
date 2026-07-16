using System.Globalization;
using System.Text;

namespace RomM.Client;

/// <summary>Query parameters for GET /api/roms.</summary>
public sealed class RomListQuery
{
    public string? SearchTerm { get; set; }
    public IReadOnlyList<int>? PlatformIds { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
    public string? OrderBy { get; set; }
    public string? OrderDir { get; set; }
    public bool? Matched { get; set; }
    public bool? Favorite { get; set; }
    public bool? Missing { get; set; }
    public bool? WithFiles { get; set; }

    /// <summary>Builds the query string (without leading '?').</summary>
    public string ToQueryString()
    {
        var sb = new StringBuilder();
        void Add(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(name));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        void AddBool(string name, bool? value)
        {
            if (value is null)
            {
                return;
            }

            Add(name, value.Value ? "true" : "false");
        }

        Add("search_term", SearchTerm);
        if (PlatformIds is { Count: > 0 })
        {
            foreach (var id in PlatformIds)
            {
                Add("platform_ids", id.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (Limit is int limit)
        {
            Add("limit", limit.ToString(CultureInfo.InvariantCulture));
        }

        if (Offset is int offset)
        {
            Add("offset", offset.ToString(CultureInfo.InvariantCulture));
        }

        Add("order_by", OrderBy);
        Add("order_dir", OrderDir);
        AddBool("matched", Matched);
        AddBool("favorite", Favorite);
        AddBool("missing", Missing);
        AddBool("with_files", WithFiles);
        return sb.ToString();
    }
}
