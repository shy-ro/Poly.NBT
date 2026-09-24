namespace Poly.NBT.Snbt;

/// <summary>Reports a syntax or value error while reading SNBT text.</summary>
public sealed class SnbtParseException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="SnbtParseException"/> class.</summary>
    /// <param name="message">The error description.</param>
    /// <param name="offset">The zero-based character offset at which the error was detected.</param>
    public SnbtParseException(string message, int offset) : base(message) => Offset = offset;

    /// <summary>Gets the zero-based character offset at which the error was detected.</summary>
    public int Offset { get; }
}
