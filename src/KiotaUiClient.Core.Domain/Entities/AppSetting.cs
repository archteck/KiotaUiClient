namespace KiotaUiClient.Core.Domain.Entities;

public class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string? Value { get; set; }
}
