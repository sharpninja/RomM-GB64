namespace RomM.Client.Tests;

public sealed class RomListQueryTests
{
    [Fact]
    public void ToQueryString_encodes_search_platform_limit_offset()
    {
        var q = new RomListQuery
        {
            SearchTerm = "Boulder Dash",
            PlatformIds = new[] { 1, 2 },
            Limit = 50,
            Offset = 10,
            OrderBy = "name",
            OrderDir = "asc",
            Matched = true,
        };

        var qs = q.ToQueryString();
        Assert.Contains("search_term=Boulder%20Dash", qs);
        Assert.Contains("platform_ids=1", qs);
        Assert.Contains("platform_ids=2", qs);
        Assert.Contains("limit=50", qs);
        Assert.Contains("offset=10", qs);
        Assert.Contains("order_by=name", qs);
        Assert.Contains("order_dir=asc", qs);
        Assert.Contains("matched=true", qs);
    }
}
