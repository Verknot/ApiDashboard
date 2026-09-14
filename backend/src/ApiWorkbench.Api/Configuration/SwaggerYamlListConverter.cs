using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ApiWorkbench.Api.Configuration;

public sealed class SwaggerYamlList
{
    public List<SwaggerYaml> Items { get; } = [];
}

public sealed class SwaggerYamlListConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(SwaggerYamlList);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
    {
        var list = new SwaggerYamlList();
        if (parser.Accept<SequenceStart>(out _))
        {
            parser.Consume<SequenceStart>();
            while (!parser.Accept<SequenceEnd>(out _))
            {
                var item = nestedObjectDeserializer(typeof(SwaggerYaml)) as SwaggerYaml;
                if (item is not null)
                {
                    list.Items.Add(item);
                }
            }

            parser.Consume<SequenceEnd>();
            return list;
        }

        var single = nestedObjectDeserializer(typeof(SwaggerYaml)) as SwaggerYaml;
        if (single is not null)
        {
            list.Items.Add(single);
        }

        return list;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer nestedObjectSerializer)
    {
        nestedObjectSerializer(value is SwaggerYamlList list ? list.Items : value, typeof(List<SwaggerYaml>));
    }
}
