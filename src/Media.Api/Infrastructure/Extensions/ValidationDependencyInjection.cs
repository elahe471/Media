
namespace Media.Api.Infrastructure.Extensions;

public static class ValidationDependencyInjection
{
    public static void AddApplicationValidation(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssemblyContaining<
            UploadMediaRequestValidator>();
    }
}