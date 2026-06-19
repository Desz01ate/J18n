using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace J18n.SourceGenerators.Utils;

internal static class PathCompat
{
    public static string GetRelativePath(string? relativeTo, string? path)
    {
        if (relativeTo == null) throw new ArgumentNullException(nameof(relativeTo));
        if (path == null) throw new ArgumentNullException(nameof(path));

        var from = TrimEndSep(Path.GetFullPath(relativeTo));
        var to = TrimEndSep(Path.GetFullPath(path));

        var comp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(Path.GetPathRoot(from), Path.GetPathRoot(to), comp))
            return to; // different volumes/roots → cannot be relative

        var fromSeg = Split(from);
        var toSeg = Split(to);

        int i = 0, max = Math.Min(fromSeg.Length, toSeg.Length);

        for (; i < max && string.Equals(fromSeg[i], toSeg[i], comp); i++) { }

        var ups = fromSeg.Length - i;
        if (ups == 0 && i == toSeg.Length) return ".";

        var sb = new StringBuilder();
        for (var u = 0; u < ups; u++) Append(ref sb, "..");
        for (var t = i; t < toSeg.Length; t++) Append(ref sb, toSeg[t]);
        return sb.Length == 0 ? "." : sb.ToString();

        static string TrimEndSep(string p) => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static string[] Split(string p) => p.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        static void Append(ref StringBuilder sb, string seg)
        {
            if (sb.Length > 0) sb.Append(Path.DirectorySeparatorChar);
            sb.Append(seg);
        }
    }
}