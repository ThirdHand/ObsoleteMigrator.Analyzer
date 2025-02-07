// ReSharper disable once CheckNamespace
namespace LightJson.Serialization;

/// <summary>
/// Represents a position within a plain text resource.
/// </summary>
internal struct TextPosition
{
    /// <summary>
    /// The column position, 0-based.
    /// </summary>
    public long Column;

    /// <summary>
    /// The line position, 0-based.
    /// </summary>
    public long Line;
}