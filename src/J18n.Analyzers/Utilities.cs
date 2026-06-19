using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace J18n.Analyzers;

public static class Utilities
{
    private static bool IsStringLiteralConstant(IOperation operation, out string? value)
    {
        value = null;

        if (operation is ILiteralOperation { Type.SpecialType: SpecialType.System_String } literal)
        {
            value = literal.ConstantValue.Value as string;
            return value != null;
        }

        return false;
    }

    public static bool IsLocalizationAccessor(IOperation operation, LocalizationConfig config)
    {
        switch (operation)
        {
            case IInvocationOperation invocation:
                return IsLocalizationMethod(invocation, config);

            case IPropertyReferenceOperation { Arguments.Length: > 0 } propertyRef:
                return IsLocalizationIndexer(propertyRef, config);

            default:
                return false;
        }
    }

    private static bool IsLocalizationMethod(IInvocationOperation invocation, LocalizationConfig config)
    {
        var methodSymbol = invocation.TargetMethod;

        foreach (var accessor in config.AccessorKinds)
        {
            if (accessor.Type != AccessorType.Method) continue;

            foreach (var methodName in accessor.TypesOrMethods)
            {
                if (methodSymbol.Name.Equals(methodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLocalizationIndexer(IPropertyReferenceOperation propertyRef, LocalizationConfig config)
    {
        var propertySymbol = propertyRef.Property;

        // Check if this is an indexer property
        if (!propertySymbol.IsIndexer) return false;

        var containingType = propertySymbol.ContainingType;

        foreach (var accessor in config.AccessorKinds)
        {
            if (accessor.Type != AccessorType.Indexer) continue;

            foreach (var typeName in accessor.TypesOrMethods)
            {
                if (IsTypeOrImplementsInterface(containingType, typeName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTypeOrImplementsInterface(ITypeSymbol type, string typeName)
    {
        // Check exact type name match
        if (type.Name.Equals(typeName, StringComparison.Ordinal) ||
            type.ToDisplayString().Contains(typeName))
        {
            return true;
        }

        // Check interfaces
        foreach (var @interface in type.AllInterfaces)
        {
            if (@interface.Name.Equals(typeName, StringComparison.Ordinal) ||
                @interface.ToDisplayString().Contains(typeName))
            {
                return true;
            }
        }

        return false;
    }

    public static string? ExtractKeyFromArgument(IOperation argumentOperation)
    {
        if (IsStringLiteralConstant(argumentOperation, out var literalValue))
        {
            return literalValue;
        }

        // Could add support for interpolated strings without expressions here

        return null;
    }

    public static Location? GetStringLiteralLocation(IOperation operation)
    {
        if (operation.Syntax is LiteralExpressionSyntax literal)
        {
            return literal.GetLocation();
        }

        return operation.Syntax.GetLocation();
    }

    internal static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a))
        {
            return b.Length;
        }

        if (string.IsNullOrWhiteSpace(b))
        {
            return a.Length;
        }

        var matrix = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] =
                    Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1, // deletion
                            matrix[i, j - 1] + 1), // insertion
                        matrix[i - 1, j - 1] + cost); // substitution
            }
        }

        return matrix[a.Length, b.Length];
    }
}