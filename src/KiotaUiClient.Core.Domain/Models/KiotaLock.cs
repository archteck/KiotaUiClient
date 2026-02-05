using System.Text.Json.Serialization;

namespace KiotaUiClient.Core.Domain.Models;

public class KiotaLock
{
    [JsonPropertyName("descriptionLocation")]
    public string DescriptionLocation { get; set; } = string.Empty;
    [JsonPropertyName("clientNamespaceName")]
    public string ClientNamespaceName { get; set; } = string.Empty;
    [JsonPropertyName("clientClassName")]
    public string ClientClassName { get; set; } = string.Empty;
    [JsonPropertyName("language")]
    public string Language { get; set; } = "CSharp";
    [JsonPropertyName("typeAccessModifier")]
    public string TypeAccessModifier { get; set; } = "Public";
}
