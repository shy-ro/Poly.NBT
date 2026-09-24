using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
    public NbtList(params ReadOnlySpan<NbtElement> items) => _items = items.ToArray();
    public int Count => _items.Count;
    public NbtElement this[int index] => _items[index];
    public IEnumerator<NbtElement> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool TryToArray([NotNullWhen(true)] out byte[]? values)
        => TryToArray<NbtByte, byte>(out values, static item => unchecked((byte)item.Value));
    public bool TryToArray([NotNullWhen(true)] out sbyte[]? values)
        => TryToArray<NbtByte, sbyte>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out short[]? values)
        => TryToArray<NbtShort, short>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out int[]? values)
        => TryToArray<NbtInt, int>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out long[]? values)
        => TryToArray<NbtLong, long>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out float[]? values)
        => TryToArray<NbtFloat, float>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out double[]? values)
        => TryToArray<NbtDouble, double>(out values, static item => item.Value);
    public bool TryToArray([NotNullWhen(true)] out string[]? values)
        => TryToArray<NbtString, string>(out values, static item => item.Value);
    public bool Equals(NbtList? other) => other is not null && this.SequenceEqual(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (NbtElement item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    private bool TryToArray<TElement, TValue>([NotNullWhen(true)] out TValue[]? values, Func<TElement, TValue> selector)
        where TElement : NbtElement
    {
        TValue[] result = new TValue[Count];
        for (int i = 0; i < result.Length; i++)
        {
            if (_items[i] is not TElement item)
            {
                values = null;
                return false;
            }
            result[i] = selector(item);
        }

        values = result;
        return true;
    }
}

/// <summary>An NBT compound: a string-keyed map of elements that preserves insertion order.</summary>
/// <remarks>
/// <para>
/// Enumeration follows insertion order, and that order is part of the contract rather than an artifact of the
/// backing collection. Both of this type's consumers depend on it: <see cref="Snbt.SnbtWriter"/> emits
/// properties in enumeration order, so the order decides the text, and the binary writer emits them in
/// enumeration order, so it decides the bytes. A plain <see cref="Dictionary{TKey,TValue}"/> happens to
/// enumerate in insertion order while nothing is removed, but that is not documented and not promised, so the
/// backing store is a <see cref="OrderedDictionary{TKey,TValue}"/>, which guarantees it.
/// </para>
/// <para>
/// Order does not affect value equality. <see cref="Equals(NbtCompound?)"/> compares key by key and
/// <see cref="GetHashCode"/> hashes the entries in sorted key order, so two compounds with the same entries in
/// different orders are equal, as NBT's own semantics require.
/// </para>
/// </remarks>
public sealed record NbtCompound : NbtElement, IReadOnlyDictionary<string, NbtElement>
{
    private readonly IReadOnlyDictionary<string, NbtElement> _entries;

    public NbtCompound(IEnumerable<KeyValuePair<string, NbtElement>> entries)
        => _entries = new OrderedDictionary<string, NbtElement>(
            entries ?? throw new ArgumentNullException(nameof(entries)), StringComparer.Ordinal);

    public NbtCompound(params ReadOnlySpan<KeyValuePair<string, NbtElement>> entries)
        => _entries = new OrderedDictionary<string, NbtElement>(entries.ToArray(), StringComparer.Ordinal);
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
