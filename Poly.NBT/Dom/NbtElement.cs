using System.Collections;
using PolyType;

namespace Poly.NBT.Dom;

[GenerateShape]
public abstract partial record NbtElement;

public sealed record NbtByte(sbyte Value) : NbtElement;
public sealed record NbtShort(short Value) : NbtElement;
public sealed record NbtInt(int Value) : NbtElement;
public sealed record NbtLong(long Value) : NbtElement;
public sealed record NbtFloat(float Value) : NbtElement;
public sealed record NbtDouble(double Value) : NbtElement;
public sealed record NbtString(string Value) : NbtElement;
public sealed record NbtByteArray(byte[] Value) : NbtElement
{
    public bool Equals(NbtByteArray? other) => other is not null && Value.AsSpan().SequenceEqual(other.Value);
    public override int GetHashCode() => StructuralHash(Value);
    private static int StructuralHash(ReadOnlySpan<byte> values)
    {
        var hash = new HashCode();
        hash.AddBytes(values);
        return hash.ToHashCode();
    }
}

public sealed record NbtIntArray(int[] Value) : NbtElement
{
    public bool Equals(NbtIntArray? other) => other is not null && Value.AsSpan().SequenceEqual(other.Value);
    public override int GetHashCode() => StructuralHash(Value);
    private static int StructuralHash(ReadOnlySpan<int> values)
    {
        var hash = new HashCode();
        hash.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));
        return hash.ToHashCode();
    }
}

public sealed record NbtLongArray(long[] Value) : NbtElement
{
    public bool Equals(NbtLongArray? other) => other is not null && Value.AsSpan().SequenceEqual(other.Value);
    public override int GetHashCode() => StructuralHash(Value);
    private static int StructuralHash(ReadOnlySpan<long> values)
    {
        var hash = new HashCode();
        hash.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));
        return hash.ToHashCode();
    }
}

public sealed record NbtList : NbtElement, IReadOnlyList<NbtElement>
{
    private readonly IReadOnlyList<NbtElement> _items;

    public NbtList(IEnumerable<NbtElement> items) => _items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
    public NbtList(params NbtElement[] items) : this((IEnumerable<NbtElement>)items) { }
    public int Count => _items.Count;
    public NbtElement this[int index] => _items[index];
    public IEnumerator<NbtElement> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(NbtList? other) => other is not null && this.SequenceEqual(other);
    public override int GetHashCode() => this.Aggregate(new HashCode(), (hash, value) => { hash.Add(value); return hash; }).ToHashCode();
}

public sealed record NbtCompound : NbtElement, IReadOnlyDictionary<string, NbtElement>
{
    private readonly IReadOnlyDictionary<string, NbtElement> _entries;

    public NbtCompound(IEnumerable<KeyValuePair<string, NbtElement>> entries)
        => _entries = entries?.ToDictionary(StringComparer.Ordinal) ?? throw new ArgumentNullException(nameof(entries));

    public NbtCompound(params KeyValuePair<string, NbtElement>[] entries) : this((IEnumerable<KeyValuePair<string, NbtElement>>)entries) { }
    public int Count => _entries.Count;
    public IEnumerable<string> Keys => _entries.Keys;
    public IEnumerable<NbtElement> Values => _entries.Values;
    public NbtElement this[string key] => _entries[key];
    public bool ContainsKey(string key) => _entries.ContainsKey(key);
    public bool TryGetValue(string key, out NbtElement value) => _entries.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, NbtElement>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(NbtCompound? other)
        => other is not null && Count == other.Count && this.All(entry => other.TryGetValue(entry.Key, out NbtElement value) && Equals(entry.Value, value));
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach ((string key, NbtElement value) in this.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}
