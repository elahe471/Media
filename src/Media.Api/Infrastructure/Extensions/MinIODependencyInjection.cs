using Minio;
using Minio.AspNetCore;

namespace Media.Api.Infrastructure.Extensions
{
    public static class MinIODependencyInjection
    {
        public static void AddMinIO(this IHostApplicationBuilder builder)
        {
            var endpoint = builder.Configuration["MinIO:Endpoint"]
                ?? throw new InvalidOperationException("MinIO Endpoint is not configured.");

            var accessKey = builder.Configuration["MinIO:AccessKey"]
                ?? throw new InvalidOperationException("MinIO AccessKey is not configured.");

            var secretKey = builder.Configuration["MinIO:SecretKey"]
                ?? throw new InvalidOperationException("MinIO SecretKey is not configured.");

            builder.Services.AddMinio(configureClient =>
                configureClient
                    .WithEndpoint(endpoint)
                    .WithCredentials(accessKey, secretKey)
                      .WithSSL(false)
                    .Build());
        }
    }
}
