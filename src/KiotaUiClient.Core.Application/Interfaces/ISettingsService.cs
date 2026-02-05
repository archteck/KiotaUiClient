namespace KiotaUiClient.Core.Application.Interfaces;

public interface ISettingsService
{
    double GetDouble(string key, double defaultValue);
    void SetDouble(string key, double value);
}
