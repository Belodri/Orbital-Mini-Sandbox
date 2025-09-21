namespace Attributes;

/// <summary>
/// An attribute that triggers the generation of a layout record.
/// The generated record will have the same field names but with all types as 'int'.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateLayoutRecord : Attribute
{
}