namespace KiotaUiClient.Core.Application.Interfaces;

public interface ISettingsService
{
    Task<double> GetDoubleAsync(string key, double defaultValue);
    Task SetDoubleAsync(string key, double value);
}
