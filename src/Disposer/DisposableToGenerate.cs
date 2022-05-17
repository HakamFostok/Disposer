namespace Disposer;

public readonly struct DisposableToGenerate
{
    public readonly string Name;
    public readonly string Namespace;

    public DisposableToGenerate(
        string name,
        string ns)
    {
        Name = name;
        Namespace = ns;
    }
}