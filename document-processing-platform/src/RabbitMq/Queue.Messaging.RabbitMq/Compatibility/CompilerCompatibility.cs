#if NET48

using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }

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
            FeatureName = featureName;
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