namespace Poly.NBT.Dom;

public sealed record NbtDocument
{
    public NbtDocument(string rootTagName, NbtElement rootElement)
    {
        ArgumentNullException.ThrowIfNull(rootTagName);
        ArgumentNullException.ThrowIfNull(rootElement);
        RootTagName = rootTagName;
        RootElement = rootElement;
    }

    public string RootTagName { get; }
    public NbtElement RootElement { get; }
}
