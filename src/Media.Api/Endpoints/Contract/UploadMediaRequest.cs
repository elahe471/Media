namespace Media.Api.Endpoints.Contract
{
    public sealed record UploadMediaRequest(
      string BucketName,
      IFormFile File);
}
