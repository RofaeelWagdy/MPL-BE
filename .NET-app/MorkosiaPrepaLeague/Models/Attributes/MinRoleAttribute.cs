using System;
using MorkosiaPrepaLeague.Models.Core;

namespace MorkosiaPrepaLeague.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MinRoleAttribute : Attribute
    {
        public Role Required { get; }
        public MinRoleAttribute(Role required) => Required = required;
    }
}