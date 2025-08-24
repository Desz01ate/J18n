using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace J18n.SourceGenerators;

public sealed class ResourceItem
{
    private readonly static Regex CultureSuffixRegex = new(@"^(?<name>.+?)\.(?<culture>[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*)$", RegexOptions.Compiled);

    private readonly static char[] InvalidChars = ['-', ' ', '.'];

    private readonly static HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    };

    public string AbsolutePath { get; }

    public string RelativeDirFromRoot { get; }

    public string BaseName { get; }

    public string ClassName { get; }

    public string Namespace { get; }

    public string HintName { get; }
    
    public JsonObjectNode? JsonStructure { get; }

    public ResourceItem(string absolutePath, string relativeDirFromRoot, string baseName, string className, string namespaceName, string hintName, JsonObjectNode? jsonStructure = null)
    {
        this.AbsolutePath = absolutePath;
        this.RelativeDirFromRoot = relativeDirFromRoot;
        this.BaseName = baseName;
        this.ClassName = className;
        this.Namespace = namespaceName;
        this.HintName = hintName;
        this.JsonStructure = jsonStructure;
    }

    public static ResourceItem? TryCreate(AdditionalText additionalText, AnalyzerConfigOptions globalOptions)
    {
        const string resourceRoot = "Resources";
        const string resourcesWithSlash = $"/{resourceRoot}/";
        const bool applyPascalCase = true;

        var filePath = additionalText.Path;

        if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rootNamespace = globalOptions.TryGetValue("build_property.RootNamespace", out var ns) ? ns : string.Empty;
        globalOptions.TryGetValue("build_property.ProjectDir", out var projectDir);

        // Normalize path separators
        var normalizedPath = filePath.Replace('\\', '/');
        var relativePathFromProject = GetRelativePathFromProject(normalizedPath, projectDir);

        // Determine if under top-level Resources or somewhere else
        string relativeDirFromRoot;
        string namespaceSuffix;

        if (relativePathFromProject.StartsWith($"{resourceRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            // Compute parts under Resources/ for relativeDirFromRoot to keep prior behavior
            var afterResources = relativePathFromProject.Substring(resourceRoot.Length + 1);
            var lastSlashIndex = afterResources.LastIndexOf('/');
            relativeDirFromRoot = lastSlashIndex > 0
                ? afterResources.Substring(0, lastSlashIndex).Replace('/', '.')
                : string.Empty;

            // Namespace includes Resources plus subfolders under it
            namespaceSuffix = string.IsNullOrEmpty(relativeDirFromRoot)
                ? resourceRoot
                : $"{resourceRoot}.{relativeDirFromRoot}";
        }
        else
        {
            // Use full folder path (without file name) relative to project root
            var lastSlashIndex = relativePathFromProject.LastIndexOf('/');
            var folderOnly = lastSlashIndex > 0 ? relativePathFromProject.Substring(0, lastSlashIndex) : string.Empty;
            relativeDirFromRoot = folderOnly.Replace('/', '.');
            namespaceSuffix = relativeDirFromRoot;
        }

        // Extract base name (strip extension and culture suffix)
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var baseName = fileName;

        var match = CultureSuffixRegex.Match(fileName);

        if (match.Success)
        {
            baseName = match.Groups["name"].Value;
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        // Sanitize class name
        var className = SanitizeIdentifier(baseName, applyPascalCase);

        // Build namespace: RootNamespace[.namespaceSuffix]
        string namespaceName;

        if (string.IsNullOrEmpty(rootNamespace))
        {
            namespaceName = namespaceSuffix;
        }
        else if (string.IsNullOrEmpty(namespaceSuffix))
        {
            namespaceName = rootNamespace;
        }
        else
        {
            // Sanitize each segment of the suffix
            var segments = namespaceSuffix.Split('.');
            var sanitizedSegments = segments.Where(s => !string.IsNullOrWhiteSpace(s))
                                            .Select(s => SanitizeIdentifier(s, applyPascalCase));
            namespaceName = $"{rootNamespace}.{string.Join(".", sanitizedSegments)}";
        }

        // Parse JSON content
        JsonObjectNode? jsonStructure = null;
        try
        {
            var sourceText = additionalText.GetText();
            if (sourceText != null)
            {
                var content = sourceText.ToString();
                using var document = JsonDocument.Parse(content);
                jsonStructure = JsonStructureParser.ParseJsonElement(document.RootElement, className);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON - continue without structure
        }

        // Create hint name
        var hintPath = string.IsNullOrEmpty(relativeDirFromRoot)
            ? className
            : $"{relativeDirFromRoot.Replace('.', '/')}/{className}";
        var hintName = $"LocalizationMarkers/{hintPath}.g.cs";

        return new ResourceItem(filePath, relativeDirFromRoot, baseName, className, namespaceName, hintName, jsonStructure);
    }

    private static string GetRelativePathFromProject(string normalizedPath, string? projectDir)
    {
        // If path is already relative (no leading slash or drive letter), use as-is
        var isRooted = Path.IsPathRooted(normalizedPath) || normalizedPath.StartsWith("/", StringComparison.Ordinal);

        if (!isRooted)
        {
            return normalizedPath.TrimStart('/');
        }

        if (!string.IsNullOrEmpty(projectDir))
        {
            try
            {
                var rel = Utils.PathCompat.GetRelativePath(projectDir, normalizedPath)
                               .Replace('\\', '/');
                return rel.TrimStart('/');
            }
            catch
            {
                // ignore and fall back
            }
        }

        // Fallback: attempt to locate "/Resources/" marker, else return filename only folder
        var idx = normalizedPath.IndexOf("/", StringComparison.Ordinal);
        return idx >= 0 ? normalizedPath.Substring(idx + 1) : normalizedPath;
    }

    private static string SanitizeIdentifier(string name, bool applyPascalCase)
    {
        if (string.IsNullOrEmpty(name))
            return "_";

        var result = name;

        // Replace invalid characters
        foreach (var invalidChar in InvalidChars)
        {
            result = result.Replace(invalidChar, '_');
        }

        // Apply PascalCase if requested
        if (applyPascalCase && !IsPascalCase(result))
        {
            var parts = result.Split(['_', '-', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
            result = string.Join("", Array.ConvertAll(parts, part =>
                string.IsNullOrEmpty(part) ? string.Empty : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        // Ensure it starts with letter or underscore
        if (!char.IsLetter(result[0]) && result[0] != '_')
        {
            result = "_" + result;
        }

        // Handle keywords
        if (CSharpKeywords.Contains(result))
        {
            result += "_";
        }

        return result;
    }

    private static bool IsPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        return char.IsUpper(input[0]) && !input.Contains("_") && !input.Contains("-") && !input.Contains(" ");
    }
}