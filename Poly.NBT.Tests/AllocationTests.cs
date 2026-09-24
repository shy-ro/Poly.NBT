using PolyType.ReflectionProvider;

namespace Poly.NBT.Tests;

/// <summary>
/// Pins the allocation behavior of the hot read path.
/// </summary>
/// <remarks>
/// Every measurement is a delta between two payload sizes rather than an absolute figure. A single
/// <c>Deserialize</c> call also pays a fixed cost for the PolyType converter-cache lookup, which is unrelated to
/// the codecs; differencing two payload sizes cancels that cost and leaves only the per-element allocation. The
/// thresholds are deliberately loose: they catch a reintroduced per-scalar buffer, not a few bytes of noise.
/// </remarks>
public sealed class AllocationTests
{
    private const int Iterations = 20;

    [Fact]
    public void ReadingIntArraysDoesNotAllocatePerElement()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        var shape = ReflectionTypeShapeProvider.Default.GetTypeShape<int[]>();

        long small = Measure(20);
        long large = Measure(200);

        // Each element must cost no more than its own four bytes in the result array. The previous
        // implementation additionally rented a byte[4] per element, which showed up as ~28 bytes here.
        double perElement = (large - small) / 180.0;
        Assert.True(perElement < 8, $"Deserializing an int array allocated {perElement:N1} bytes per element.");

        int[] Payload(int count)
        {
            int[] values = new int[count];
            for (int index = 0; index < count; index++) values[index] = index;
            return values;
        }

        long Measure(int count)
        {
            byte[] payload = serializer.SerializeUsingReflection(Payload(count), "");
            using var stream = new MemoryStream(payload, writable: false);
            long consumed = 0;
            for (int index = 0; index < 3; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Length;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < Iterations; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Length;
            }

            GC.KeepAlive(consumed);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        }
    }

    [Fact]
    public void ReadingStringsDoesNotAllocateAnIntermediateBuffer()
    {
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
        var shape = ReflectionTypeShapeProvider.Default.GetTypeShape<string>();

        long small = Measure(1024);
        long large = Measure(4096);

        // The decoded string costs two bytes per character. An extra byte[] per string would push this to
        // three bytes per character regardless of the buffer being stack- or pool-based.
        double perCharacter = (large - small) / 3072.0;
        Assert.True(perCharacter < 2.5, $"Deserializing a string allocated {perCharacter:N2} bytes per character.");

        long Measure(int length)
        {
            byte[] payload = serializer.SerializeUsingReflection(new string('a', length), "");
            using var stream = new MemoryStream(payload, writable: false);
            long consumed = 0;
            for (int index = 0; index < 3; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Length;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < Iterations; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Length;
            }

            GC.KeepAlive(consumed);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        }
    }

    [Fact]
    public void ReadingListElementsDoesNotAllocatePerScalar()
    {
        // A dialect without primitive-array optimization keeps the elements in a TAG_List, so every element
        // travels through the scalar codec instead of the bulk array path.
        NbtSerializer serializer = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { OptimizePrimitiveListsToArrays = false });
        var shape = ReflectionTypeShapeProvider.Default.GetTypeShape<List<int>>();

        long small = Measure(20);
        long large = Measure(200);

        // List<int> reserves exactly four bytes per element; the numeric codec must not add to that.
        double perElement = (large - small) / 180.0;
        Assert.True(perElement < 8, $"Reading a 200-element list allocated {perElement:N1} bytes per element.");

        long Measure(int count)
        {
            List<int> values = [.. Enumerable.Range(0, count)];
            byte[] payload = serializer.SerializeUsingReflection(values, "");
            using var stream = new MemoryStream(payload, writable: false);
            long consumed = 0;
            for (int index = 0; index < 3; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Count;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < Iterations; index++)
            {
                stream.Position = 0;
                consumed += serializer.Deserialize(stream, shape).Count;
            }

            GC.KeepAlive(consumed);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        }
    }
}
