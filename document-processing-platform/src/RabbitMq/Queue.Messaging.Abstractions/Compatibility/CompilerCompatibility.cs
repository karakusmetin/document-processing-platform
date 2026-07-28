#if NET48

using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Enables init-only properties when compiling for
    /// .NET Framework 4.8.
    /// </summary>
    internal static class IsExternalInit
    {
    }

    /// <summary>
    /// Compiler metadata required for C# required members.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Field |
        AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute :
        Attribute
    {
    }

    /// <summary>
    /// Identifies language features required by a compiled type.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.All,
        AllowMultiple = true,
        Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute :
        Attribute
    {
        public CompilerFeatureRequiredAttribute(
            string featureName)
        {
            FeatureName =
                featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; set; }

        public const string RefStructs =
            nameof(RefStructs);

        public const string RequiredMembers =
            nameof(RequiredMembers);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Compiler metadata for constructors that initialize
    /// required members.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Constructor,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute :
        Attribute
    {
    }
}

#endif