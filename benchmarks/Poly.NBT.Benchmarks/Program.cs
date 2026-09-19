using System.Diagnostics;
using Poly.NBT;

int[] array = Enumerable.Range(0, 4096).ToArray();
List<int> list = [.. array];
string ascii = new('a', 4096);
Dictionary<string, int> dictionary = Enumerable.Range(0, 256).ToDictionary(i => $"k{i}", i => i);
NbtSerializer optimized = NbtSerializer.Create(NbtOptions.JavaNetworkEdition);
NbtSerializer generic = NbtSerializer.Create(NbtOptions.JavaNetworkEdition with { OptimizePrimitiveListsToArrays = false });

Run("int[] bulk", () => optimized.SerializeUsingReflection(array));
Run("List<int> generic", () => generic.SerializeUsingReflection(list));
Run("Modified UTF-8 ASCII fast", () => EncodeModifiedUtf8Fast(ascii));
Run("Modified UTF-8 old loop", () => EncodeModifiedUtf8Baseline(ascii));
Run("dictionary direct", () => Consume(dictionary));
Run("dictionary copy", () => Consume(dictionary.ToDictionary(static p => p.Key, static p => p.Value)));

static void Run(string name, Action action)
{
    const int iterations = 2_000;
    for (int i = 0; i < 100; i++) action();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    Stopwatch stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++) action();
    stopwatch.Stop();
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine($"{name,-28} {stopwatch.Elapsed.TotalMilliseconds,9:F2} ms  {allocated / iterations,9:N0} B/op");
}

static int Consume(IEnumerable<KeyValuePair<string, int>> values)
{
    int result = 0;
    foreach (KeyValuePair<string, int> value in values) result ^= value.Value;
    return result;
}

static byte[] EncodeModifiedUtf8Baseline(string value)
{
    int length = 0;
    foreach (char character in value) length += character is >= '\u0001' and <= '\u007f' ? 1 : character <= '\u07ff' ? 2 : 3;
    byte[] result = GC.AllocateUninitializedArray<byte>(length);
    int offset = 0;
    foreach (char character in value)
    {
        if (character is >= '\u0001' and <= '\u007f') result[offset++] = (byte)character;
        else if (character <= '\u07ff')
        {
            result[offset++] = (byte)(0xc0 | character >> 6);
            result[offset++] = (byte)(0x80 | character & 0x3f);
        }
        else
        {
            result[offset++] = (byte)(0xe0 | character >> 12);
            result[offset++] = (byte)(0x80 | character >> 6 & 0x3f);
            result[offset++] = (byte)(0x80 | character & 0x3f);
        }
    }
    return result;
}

static byte[] EncodeModifiedUtf8Fast(string value)
{
    if (value.AsSpan().IndexOfAnyExceptInRange('\u0001', '\u007f') >= 0) return EncodeModifiedUtf8Baseline(value);
    byte[] result = GC.AllocateUninitializedArray<byte>(value.Length);
    for (int i = 0; i < value.Length; i++) result[i] = (byte)value[i];
    return result;
}
