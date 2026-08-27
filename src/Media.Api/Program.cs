using Media.Api.Infrastructure.Extensions;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddMinIO();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", async (IMinioClient minioClient) =>
{
    var result = await minioClient.ListBucketsAsync();

    var bucketNames = result.Buckets
        .Select(x => x.Name)
        .ToList();

    return Results.Ok(bucketNames);
});

app.Run();