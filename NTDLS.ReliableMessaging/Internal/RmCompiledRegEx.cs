using System.Text.RegularExpressions;

namespace NTDLS.ReliableMessaging.Internal
{
    internal partial class RmCompiledRegEx
    {
        [GeneratedRegex(@"(,?\s*Version\s*=\s*[\d.]+)|(,?\s*Culture\s*=\s*[^,]+)|(,?\s*PublicKeyToken\s*=\s*[^,\]]+)")]
        internal static partial Regex TypeTagsRegex();

        [GeneratedRegex(@"\s*,\s*")]
        internal static partial Regex TypeCleanupRegex();
    }
}