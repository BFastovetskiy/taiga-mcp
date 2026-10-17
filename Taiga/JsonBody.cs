using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaigaMcp.Taiga;

/// <summary>
/// Вспомогательный построитель JSON-тел запросов.
/// Позволяет удобно собирать объект и автоматически пропускает null-значения,
/// чтобы не перезаписывать поля Taiga пустыми значениями при PATCH.
/// </summary>
public static class JsonBody
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Создаёт JsonObject из набора пар «имя — значение», пропуская null.</summary>
    public static JsonObject Object(params (string Name, object? Value)[] items)
    {
        var obj = new JsonObject();
        foreach (var (name, value) in items)
        {
            if (value is null)
            {
                continue;
            }

            obj[name] = ToNode(value);
        }

        return obj;
    }

    /// <summary>Преобразует произвольное значение в JsonNode.</summary>
    public static JsonNode? ToNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node.DeepClone();
        }

        return JsonSerializer.SerializeToNode(value, Options);
    }
}
