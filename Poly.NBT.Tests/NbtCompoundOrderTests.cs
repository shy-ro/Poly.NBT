using Poly.NBT.Dom;
using Poly.NBT.Snbt;

namespace Poly.NBT.Tests;

/// <summary>
/// Pins the compound key order as a contract rather than an artifact of the backing collection.
/// </summary>
/// <remarks>
/// Both of <see cref="NbtCompound"/>'s consumers depend on enumeration order: the SNBT writer emits properties
/// in that order, so it decides the text, and the binary writer emits them in that order, so it decides the
/// bytes. A plain <see cref="Dictionary{TKey,TValue}"/> happens to enumerate in insertion order while nothing is
/// removed, but that is neither documented nor promised, so the order is asserted here.
/// </remarks>
public sealed class NbtCompoundOrderTests
{
    private static readonly string[] Keys = ["zeta", "alpha", "mu", "beta"];

    [Fact]
    public void EveryConstructionRouteKeepsTheGivenOrder()
    {
        KeyValuePair<string, NbtElement>[] entries =
        [
            KeyValuePair.Create("zeta", (NbtElement)new NbtInt(1)),
            KeyValuePair.Create("alpha", (NbtElement)new NbtInt(2)),
            KeyValuePair.Create("mu", (NbtElement)new NbtInt(3)),
            KeyValuePair.Create("beta", (NbtElement)new NbtInt(4)),
        ];

        Assert.Equal(Keys, new NbtCompound(entries).Keys);
        Assert.Equal(Keys, new NbtCompound(entries.AsSpan()).Keys);

        // A dictionary source contributes its own enumeration order; what this pins is that the compound does not
        // reorder it on the way in.
        Dictionary<string, NbtElement> dictionary = new(StringComparer.Ordinal);
        foreach ((string key, NbtElement value) in entries) dictionary[key] = value;
        Assert.Equal(Keys, new NbtCompound(dictionary).Keys);

        // And that the order survives the decode path, which builds its own dictionary as it reads.
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        NbtCompound restored = Assert.IsType<NbtCompound>(
            serializer.DeserializeDocument(serializer.Serialize(new NbtDocument(string.Empty, new NbtCompound(entries)))).RootElement);
        Assert.Equal(Keys, restored.Keys);
        Assert.Equal(Keys, restored.Select(entry => entry.Key));
    }

    [Fact]
    public void OrderAffectsTheBytesAndTheTextButNotEquality()
    {
        NbtCompound forward = new(
            KeyValuePair.Create("z", (NbtElement)new NbtInt(1)),
            KeyValuePair.Create("a", (NbtElement)new NbtInt(2)));
        NbtCompound reversed = new(
            KeyValuePair.Create("a", (NbtElement)new NbtInt(2)),
            KeyValuePair.Create("z", (NbtElement)new NbtInt(1)));

        // The order is observable in both outputs, which is why it has to be a contract.
        Assert.Equal("{z:1,a:2}", SnbtWriter.Write(forward));
        Assert.Equal("{a:2,z:1}", SnbtWriter.Write(reversed));

        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        Assert.NotEqual(serializer.Serialize(new NbtDocument(string.Empty, forward)), serializer.Serialize(new NbtDocument(string.Empty, reversed)));

        // NBT itself treats a compound as an unordered map, so equality and hashing must stay order-independent.
        Assert.Equal(forward, reversed);
        Assert.Equal(forward.GetHashCode(), reversed.GetHashCode());
    }

    [Fact]
    public void DuplicateKeysAreRejected()
    {
        // ToDictionary threw ArgumentException, and the replacement has to keep doing so rather than silently
        // keeping the last value.
        Assert.Throws<ArgumentException>(() => new NbtCompound(
            KeyValuePair.Create("a", (NbtElement)new NbtInt(1)),
            KeyValuePair.Create("a", (NbtElement)new NbtInt(2))));
    }
}
