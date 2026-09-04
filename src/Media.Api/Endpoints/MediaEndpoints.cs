
using Media.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Media.Api.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/public/{bucketName}/{catalogId}", UploadPublic)
            .DisableAntiforgery();

        app.MapPost("/private/{bucketName}/{catalogId}", UploadPrivate)
            .DisableAntiforgery();

        app.MapGet("/{tokenId:guid}", GetImageByToken);

        return app;
    }
    public static async Task<IResult> UploadPublic(
    string bucketName,
    string catalogId,
    IFormFile file,
    IValidator<UploadMediaRequest> validator,
    IMinioClient minioClient,
    IPublishEndpoint publisher,
    IOptions<AppSettings> appSettings,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PublicMediaUpload");

        // 1. Validation

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

        // 2. Generate unique Object Key

        var extension = Path.GetExtension(file.FileName);

        var objectName =
            $"{catalogId}/{Guid.NewGuid():N}{extension}";

        // 3. Upload to MinIO

        try
        {
            await using var fileStream =
                file.OpenReadStream();

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithContentType(file.ContentType)
                .WithObjectSize(file.Length)
                .WithStreamData(fileStream);

            await minioClient.PutObjectAsync(
                putObjectArgs,
                cancellationToken);
        }
        catch (MinioException ex)
        {
            logger.LogError(
                ex,
                "Failed to upload public media {ObjectName} to bucket {BucketName}.",
                objectName,
                bucketName);

            return Results.Problem(
                title: "Object storage error",
                detail: "The media file could not be stored.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        // 4. Generate permanent public URL

        var publicBaseUrl =
            appSettings.Value.MediaOptions.PublicBaseUrl
                .TrimEnd('/');

        var url =
            $"{publicBaseUrl}/{bucketName}/{objectName}";

        // 5. Publish Integration Event

        try
        {
            await publisher.Publish(
                new MediaUploadedEvent(
                    file.FileName,
                    url,
                    catalogId,
                    DateTime.UtcNow),
                cancellationToken);
        }
        catch (MassTransitException ex)
        {
            logger.LogError(
                ex,
                "Public media was uploaded but MediaUploadedEvent could not be published. ObjectName: {ObjectName}",
                objectName);

            // Prevent an orphan object
            await TryRemoveObject(
                minioClient,
                bucketName,
                objectName,
                logger,
                cancellationToken);

            return Results.Problem(
                title: "Message broker error",
                detail: "The media was uploaded, but the catalog could not be notified.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // 6. Success

        return Results.Ok(new
        {
            FileName = file.FileName,
            ObjectName = objectName,
            Url = url
        });
    }
    public static async Task<IResult> UploadPrivate(
        string bucketName,
        string catalogId,
        IFormFile file,
        IValidator<UploadMediaRequest> validator,
        MediaDbContext dbContext,
        IMinioClient minioClient,
        IPublishEndpoint publisher,
        IOptions<AppSettings> mediaOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MediaUpload");

        
        // 1. Validation
        

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

        var token = new UrlToken
        {
            Id = Guid.NewGuid(),
            BucketName = bucketName,
            ObjectName = file.FileName,
            ContentType = file.ContentType,
            ExpireOn = DateTime.UtcNow.AddMinutes(10),
            CountAccess = 0
        };

        
        // 2. Upload to MinIO
        

        try
        {
            await using var fileStream = file.OpenReadStream();

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(token.ObjectName)
                .WithContentType(file.ContentType)
                .WithObjectSize(file.Length)
                .WithStreamData(fileStream);

            await minioClient.PutObjectAsync(
                putObjectArgs,
                cancellationToken);
        }
        catch (MinioException ex)
        {
            logger.LogError(
                ex,
                "Failed to upload object {ObjectName} to bucket {BucketName}.",
                token.ObjectName,
                bucketName);

            return Results.Problem(
                title: "Object storage error",
                detail: "The media file could not be stored.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        
        // 3. Save Token
        

        try
        {
            dbContext.Tokens.Add(token);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(
                ex,
                "Failed to persist media token {TokenId}.",
                token.Id);

            // Compensating action:
            // DB failed, therefore remove the already uploaded object.
            await TryRemoveObject(
                minioClient,
                bucketName,
                token.ObjectName,
                logger,
                cancellationToken);

            return Results.Problem(
                title: "Database error",
                detail: "The media information could not be saved.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        
        // 4. Publish Integration Event
        

        var url =
            $"{mediaOptions.Value.MediaOptions.CatalogBaseUrl.TrimEnd('/')}/{token.Id}";

        try
        {
            await publisher.Publish(
                new MediaUploadedEvent(
                    file.FileName,
                    url,
                    catalogId,
                    DateTime.UtcNow),
                cancellationToken);
        }
        catch (MassTransitException ex)
        {
            logger.LogError(
                ex,
                "Media was uploaded but MediaUploadedEvent could not be published. TokenId: {TokenId}",
                token.Id);

            return Results.Problem(
                title: "Message broker error",
                detail: "The media was uploaded, but the catalog could not be notified.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        
        // 5. Success
        

        return Results.Ok(new
        {
            token.Id,
            Url = url,
            token.ExpireOn
        });
    }

    public static async Task<IResult> GetImageByToken(
        Guid tokenId,
        MediaDbContext dbContext,
        IMinioClient minioClient,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MediaDownload");

        
        // 1. Find Token
        

        UrlToken? token;

        try
        {
            token = await dbContext.Tokens.FindAsync(
                [tokenId],
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to retrieve media token {TokenId}.",
                tokenId);

            return Results.Problem(
                title: "Database error",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (token is null)
        {
            return Results.NotFound(new
            {
                Message = "Media token was not found."
            });
        }

        
        // 2. Check Expiration
        

        if (token.ExpireOn <= DateTime.UtcNow)
        {
            return Results.Problem(
                title: "Media token expired",
                detail: "This media URL is no longer valid.",
                statusCode: StatusCodes.Status410Gone);
        }

        
        // 3. Check Access Limit
        

        if (token.LimitationAccess > 0 &&
            token.CountAccess >= token.LimitationAccess)
        {
            return Results.Problem(
                title: "Media token access limit reached",
                detail: "This media URL has reached its maximum number of accesses.",
                statusCode: StatusCodes.Status410Gone);
        }

        
        // 4. Reserve Access
        

        token.CountAccess++;

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(
                ex,
                "Failed to update access count for token {TokenId}.",
                tokenId);

            return Results.Problem(
                title: "Database error",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        
        // 5. Get Object From MinIO
        

        try
        {
            httpContext.Response.ContentType =
                token.ContentType;

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(token.BucketName)
                .WithObject(token.ObjectName)
                .WithCallbackStream(
                    async (stream, ct) =>
                    {
                        await stream.CopyToAsync(
                            httpContext.Response.Body,
                            ct);
                    });

            await minioClient.GetObjectAsync(
                getObjectArgs,
                cancellationToken);

            return Results.Empty;
        }
        catch (MinioException ex)
        {
            logger.LogError(
                ex,
                "Failed to retrieve object {ObjectName} from bucket {BucketName}.",
                token.ObjectName,
                token.BucketName);

            // Undo access count because download failed.
            token.CountAccess--;

            try
            {
                await dbContext.SaveChangesAsync(
                    cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(
                    rollbackException,
                    "Failed to rollback access count for token {TokenId}.",
                    tokenId);
            }

            // Response may already have started if streaming failed midway.
            if (httpContext.Response.HasStarted)
            {
                throw;
            }

            return Results.Problem(
                title: "Object storage error",
                detail: "The requested media could not be retrieved.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task TryRemoveObject(
        IMinioClient minioClient,
        string bucketName,
        string objectName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await minioClient.RemoveObjectAsync(
                removeObjectArgs,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Compensation failed. Object {ObjectName} could not be removed from bucket {BucketName}.",
                objectName,
                bucketName);
        }
    }
}