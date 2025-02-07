// This file contains some hacks to allow the use of "required" and record struct with netstandard2.0

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

public class RequiredMemberAttribute : Attribute;
public class CompilerFeatureRequiredAttribute : Attribute
{
    public CompilerFeatureRequiredAttribute(string name) { }
}

[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class SetsRequiredMembersAttribute : Attribute;

internal static class IsExternalInit {}