using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ApiWorkbench.Api.Configuration;

public sealed class StringOrEnvMap
{
    public string? Scalar { get; set; }
    public Dictionary<string, string> ByEnvironment { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StringOrEnvMapConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(StringOrEnvMap);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
    {
        var result = new StringOrEnvMap();
        if (parser.Accept<Scalar>(out _))
        {
            result.Scalar = parser.Consume<Scalar>().Value;
            return result;
        }

        var map = nestedObjectDeserializer(typeof(Dictionary<string, string>)) as Dictionary<string, string>;
        if (map is not null)
        {
            foreach (var (key, value) in map)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    result.ByEnvironment[key.Trim()] = value.Trim();
                }
            }
        }

        return result;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer nestedObjectSerializer)
    {
        if (value is not StringOrEnvMap map)
        {
            nestedObjectSerializer(null, typeof(string));
            return;
        }

        if (map.ByEnvironment.Count > 0)
        {
            nestedObjectSerializer(map.ByEnvironment, typeof(Dictionary<string, string>));
            return;
        }

        nestedObjectSerializer(map.Scalar, typeof(string));
    }
}
