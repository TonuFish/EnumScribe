using System;

namespace EnumScribe
{
    /// <summary>
    /// Prevents this member from being scribed. This class cannot be inherited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NoScribeAttribute : Attribute
    {
    }
}
