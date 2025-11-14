using YamlDotNet.Serialization;

namespace docker_compose_manager_back.Utils;

/// <summary>
/// Helper class for parsing YAML files, particularly Docker Compose files
/// </summary>
public static class YamlParserHelper
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Deserialize YAML content to a dictionary
    /// </summary>
    public static Dictionary<string, object>? Deserialize(string yamlContent)
    {
        try
        {
            return _deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract a list of strings from a dictionary key
    /// </summary>
    public static List<string>? ExtractStringList(Dictionary<object, object> data, string key)
    {
        if (!data.ContainsKey(key)) return null;

        var value = data[key];
        if (value is List<object> list)
        {
            return list.Select(i => i?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        return null;
    }

    /// <summary>
    /// Extract a dictionary of strings from a dictionary key
    /// </summary>
    public static Dictionary<string, string>? ExtractStringDictionary(Dictionary<object, object> data, string key)
    {
        if (!data.ContainsKey(key)) return null;

        var value = data[key];
        if (value is Dictionary<object, object> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? "",
                kvp => kvp.Value?.ToString() ?? ""
            );
        }
        return null;
    }

    /// <summary>
    /// Extract a dictionary of objects from a dictionary key
    /// </summary>
    public static Dictionary<string, object>? ExtractObjectDictionary(Dictionary<object, object> data, string key)
    {
        if (!data.ContainsKey(key)) return null;

        var value = data[key];
        if (value is Dictionary<object, object> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? "",
                kvp => kvp.Value ?? new object()
            );
        }
        return null;
    }

    /// <summary>
    /// Extract environment variables (can be dictionary or list format)
    /// </summary>
    public static Dictionary<string, string>? ExtractEnvironment(Dictionary<object, object> data)
    {
        if (!data.ContainsKey("environment")) return null;

        var value = data["environment"];

        // Environment can be either a dictionary or a list of strings
        if (value is Dictionary<object, object> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? "",
                kvp => kvp.Value?.ToString() ?? ""
            );
        }
        else if (value is List<object> list)
        {
            var envDict = new Dictionary<string, string>();
            foreach (var item in list)
            {
                string envStr = item?.ToString() ?? "";
                string[] parts = envStr.Split('=', 2);
                if (parts.Length == 2)
                {
                    envDict[parts[0]] = parts[1];
                }
                else if (parts.Length == 1)
                {
                    envDict[parts[0]] = "";
                }
            }
            return envDict;
        }

        return null;
    }
}
