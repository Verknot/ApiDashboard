using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ApiWorkbench.Api.Configuration;

/// <summary>Accepts regions: [eu, tr] or [{ code: eu, label: Europe }, …].</summary>
public sealed class RegionYamlList
{
    public List<RegionYaml> Items { get; } = [];
}

public sealed class RegionYamlListConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(RegionYamlList);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
    {
        var list = new RegionYamlList();
        if (!parser.Accept<SequenceStart>(out _))
        {
            return list;
        }

        parser.Consume<SequenceStart>();
        while (!parser.Accept<SequenceEnd>(out _))
        {
            if (parser.Accept<Scalar>(out var scalar))
            {
                parser.Consume<Scalar>();
                var code = scalar!.Value?.Trim() ?? string.Empty;
                if (code.Length > 0)
                {
                    list.Items.Add(new RegionYaml { Code = code });
                }

                continue;
            }

            var item = nestedObjectDeserializer(typeof(RegionYaml)) as RegionYaml;
            if (item is not null && !string.IsNullOrWhiteSpace(item.Code))
            {
                list.Items.Add(item);
            }
        }

        parser.Consume<SequenceEnd>();
        return list;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer nestedObjectSerializer)
    {
        nestedObjectSerializer(value is RegionYamlList list ? list.Items : value, typeof(List<RegionYaml>));
    }
}
