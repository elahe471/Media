

namespace Media.Api.Validators;

public sealed class UploadMediaRequestValidator
    : AbstractValidator<UploadMediaRequest>
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private const int MinWidth = 300;
    private const int MinHeight = 300;

    private const int MaxWidth = 4000;
    private const int MaxHeight = 4000;

    private const long MaxPixels = 16_000_000;

    private static readonly string[] AllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public UploadMediaRequestValidator()
    {
        // Chain 1 - Required fields
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("File is required.");

        RuleFor(x => x.BucketName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Bucket name is required.");

        When(x => x.File is not null, () =>
        {
            // Chain 2 - File Type
            RuleFor(x => x.File)
                .Cascade(CascadeMode.Stop)
                .Must(HaveValidExtension)
                .WithMessage(
                    $"Allowed extensions are: {string.Join(", ", AllowedExtensions)}.")
                .Must(HaveValidContentType)
                .WithMessage(
                    $"Allowed content types are: {string.Join(", ", AllowedContentTypes)}.");

            // Chain 3 - File Size
            RuleFor(x => x.File.Length)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithMessage("File cannot be empty.")
                .LessThanOrEqualTo(MaxFileSize)
                .WithMessage("File size cannot exceed 5 MB.");

            // Chain 4 - File Name
            RuleFor(x => x.File.FileName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("File name is required.")
                .MaximumLength(120)
                .WithMessage("File name cannot exceed 120 characters.");

            // Chain 5 - Real Image + Dimensions
            RuleFor(x => x.File)
                .CustomAsync(ValidateImageAsync);
        });
    }

    private static bool HaveValidExtension(IFormFile file)
    {
        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        return AllowedExtensions.Contains(extension);
    }

    private static bool HaveValidContentType(IFormFile file)
    {
        return AllowedContentTypes.Contains(
            file.ContentType,
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task ValidateImageAsync(
        IFormFile file,
        ValidationContext<UploadMediaRequest> context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();

            var imageInfo = await Image.IdentifyAsync(
                stream,
                cancellationToken);

            if (imageInfo is null)
            {
                context.AddFailure(
                    nameof(UploadMediaRequest.File),
                    "The uploaded file is not a valid image.");

                return;
            }

            if (imageInfo.Width < MinWidth ||
                imageInfo.Height < MinHeight)
            {
                context.AddFailure(
                    nameof(UploadMediaRequest.File),
                    $"Image dimensions must be at least {MinWidth}x{MinHeight}px.");
            }

            if (imageInfo.Width > MaxWidth ||
                imageInfo.Height > MaxHeight)
            {
                context.AddFailure(
                    nameof(UploadMediaRequest.File),
                    $"Image dimensions cannot exceed {MaxWidth}x{MaxHeight}px.");
            }

            var totalPixels =
                (long)imageInfo.Width * imageInfo.Height;

            if (totalPixels > MaxPixels)
            {
                context.AddFailure(
                    nameof(UploadMediaRequest.File),
                    $"Image resolution cannot exceed {MaxPixels:N0} pixels.");
            }
        }
        catch
        {
            context.AddFailure(
                nameof(UploadMediaRequest.File),
                "The uploaded file is not a valid or supported image.");
        }
    }
}