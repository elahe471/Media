using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Media.Api;
using Microsoft.Extensions.Options;
using Serilog;

public static class ElasticSearchDependencyInjection
{
    public static void AddApplicationLogging(
        this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (context, services, loggerConfiguration) =>
            {
                var appSettings = services
                    .GetRequiredService<IOptions<AppSettings>>()
                    .Value;

                var elasticOptions =
                    appSettings.ElasticSearchOptions;

                var elasticUri =
                    new Uri(elasticOptions.Host);

                loggerConfiguration
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty(
                        "service.name",
                        "Media.Api")
                    .Enrich.WithProperty(
                        "service.environment",
                        context.HostingEnvironment.EnvironmentName)
                    .WriteTo.Console()
                    .WriteTo.Elasticsearch(
                        [elasticUri],
                        options =>
                        {
                            options.DataStream =
                                new DataStreamName(
                                    "logs",
                                    "mediaapi",
                                    context.HostingEnvironment
                                        .EnvironmentName
                                        .ToLowerInvariant());
                            // Elasticsearch failure must NOT stop Media.Api
                            options.BootstrapMethod =
                                BootstrapMethod.Silent;
                        },
                        transport =>
                        {
                            transport.Authentication(
                                new BasicAuthentication(
                                    elasticOptions.UserName,
                                    elasticOptions.Password));

                            if (elasticUri.Scheme == Uri.UriSchemeHttps)
                            {
                                transport.CertificateFingerprint(
                                    elasticOptions.Fingerprint);
                            }
                        });
            });
    }
}