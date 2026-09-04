

using MassTransit;
using Media.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;
using Minio.DataModel.Args;

namespace Media.Api.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/{bucketName}/{CatalogId}", Upload)
            .DisableAntiforgery();

        app.MapGet("{tokenId:guid:required}", GetImageByToken);

        return app;
    }

    public static async Task<IResult> Upload(
        string bucketName,
        string CatalogId,
        IFormFile file,
        IValidator<UploadMediaRequest> validator,
          MediaDbContext dbContext,
        IMinioClient minioClient,
        IPublishEndpoint publisher,
        IOptions<MediaOptions> mediaOptions,
        CancellationToken cancellationToken)
    {
        var request = new UploadMediaRequest(
            bucketName,
            file);

        var validationResult =
            await validator.ValidateAsync(
                request,
                cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(
                validationResult.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(
                        x => x.Key,
                        x => x
                            .Select(error => error.ErrorMessage)
                            .ToArray()));
        }

        var putObjArg = new PutObjectArgs().WithBucket(bucketName)
                                            .WithObject(file.FileName)
                                            .WithContentType(file.ContentType)
                                            .WithObjectSize(file.Length)
                                            .WithStreamData(file.OpenReadStream());
    



      
        try
        {

            var token = new UrlToken
            {
                BucketName = bucketName,
                ObjectName = file.FileName,
                ContentType = file.ContentType,
                ExpireOn = DateTime.UtcNow.AddMinutes(10),
                Id = Guid.NewGuid()
            };
            dbContext.Tokens.Add(token);
            await dbContext.SaveChangesAsync();

            await minioClient.PutObjectAsync(putObjArg);

            var url = $"{mediaOptions.Value.CatalogBaseUrl}/{token.Id}";
            await publisher.Publish(new MediaUploadedEvent(file.FileName, url, CatalogId, DateTime.UtcNow), cancellationToken);
          
            
        }
        catch (Exception)
        {

            throw;
        }

        return Results.Ok();
    }

   public static async Task<IResult> GetImageByToken(
        Guid tokenId,
        MediaDbContext dbContext,
        IMinioClient minioClient,
        CancellationToken cancellationToken, HttpContext httpContext)
    {
        var token = await dbContext.Tokens.FindAsync(new object[] { tokenId }, cancellationToken);
        if (token == null )
        {
            return Results.NotFound();
        }
        var getObjArgs = new GetObjectArgs()
            .WithBucket(token.BucketName)
            .WithObject(token.ObjectName).WithCallbackStream(async (stream, cancellationToken) =>
            {
                httpContext.Response.ContentType = token.ContentType;
                await stream.CopyToAsync(httpContext.Response.Body, cancellationToken);
            });
        token.CountAccess++;
        await dbContext.SaveChangesAsync(cancellationToken);

        var stream = await minioClient.GetObjectAsync(getObjArgs, cancellationToken);
      

        return Results.Empty;
    }       
}