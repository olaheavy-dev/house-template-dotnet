using Common.Domain;

namespace Modules.Sample.Domain;

/// <summary>The module's first stored thing. Replace it with the module's first real aggregate.</summary>
public sealed class SampleItem : Entity
{
    public const int MaxLength = 200;

    public SampleItem(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Name { get; private set; }
}
