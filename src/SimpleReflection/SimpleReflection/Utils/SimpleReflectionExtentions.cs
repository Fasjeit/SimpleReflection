using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleReflection.Utils
{
    public static class SimpleReflectionExtentions
    {
        public static string GetSimpleReflectionTypeName(this ITypeSymbol symbol)
        {
            return symbol.ToDisplayString().Replace(".", "");
        }

        public static string GetSimpleReflectionExtentionTypeName(this ITypeSymbol symbol)
        {
            return $"{symbol.GetSimpleReflectionTypeName()}Extentions";
        }
    }
}
