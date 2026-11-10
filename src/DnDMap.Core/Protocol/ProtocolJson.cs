using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnDMap.Core.Protocol;

/// <summary>
/// Единые настройки JSON для сервера и клиентов: camelCase, перечисления строками,
/// null-поля не пишутся. Клиент на JS ожидает именно этот формат.
/// </summary>
public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
