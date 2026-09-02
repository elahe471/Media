

namespace Media.Contracts.IntegrationEvents;

public record MediaUploadedEvent(string FileName,string URL , string CatalogId,DateTime CreatedDate);
