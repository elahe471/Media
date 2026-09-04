namespace Media.Api;

public class AppSettings
{
    public required ElasticSearchOptions ElasticSearchOptions { get; set; }
    public required MediaOptions MediaOptions { get; set; }
    public required BrokerOptions BrokerOptions { get; set; }
}

public sealed class ElasticSearchOptions
{
    public required string Host { get; init; }
    public required string Fingerprint { get; init; }
    public required string UserName { get; init; }
    public required string Password { get; init; }
}


public class MediaOptions
{
    public string CatalogBaseUrl { get; set; }
}

public sealed class BrokerOptions
{
    public const string SectionName = "BrokerOptions";

    public required string Host { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
}



