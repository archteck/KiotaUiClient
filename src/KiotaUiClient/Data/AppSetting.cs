namespace KiotaUiClient.Data;

public class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string? Value { get; set; }
}
