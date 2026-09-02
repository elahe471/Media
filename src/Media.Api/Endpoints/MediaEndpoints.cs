

using Minio.DataModel.Args;

namespace Media.Api.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/{bucketName}", Upload)
            .DisableAntiforgery();

        return app;
    }

    public static async Task<IResult> Upload(
        string bucketName,
        IFormFile file,
        IValidator<UploadMediaRequest> validator,
        IMinioClient minioClient,
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
        await minioClient.PutObjectAsync(putObjArg);


        var statObjArg = new StatObjectArgs().WithBucket(bucketName)
                                            .WithObject(file.FileName);

        var objStatus = await minioClient.StatObjectAsync(statObjArg);

        return Results.Ok();
    }
}