using System.Text.RegularExpressions;

namespace Api.IntegrationTests.Support;

internal static partial class EmailBodyParser
{
    public static (string TokenId, string Token)? TryParseTokenPair(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        Match tokenId = TokenIdLine().Match(body);
        Match token = TokenLine().Match(body);
        if (tokenId.Success && token.Success)
            return (tokenId.Groups[1].Value, token.Groups[1].Value);

        Match query = QueryString().Match(body);
        if (query.Success)
            return (query.Groups[1].Value, query.Groups[2].Value);

        return null;
    }

    [GeneratedRegex(@"Token ID:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TokenIdLine();

    [GeneratedRegex(@"Token:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TokenLine();

    [GeneratedRegex(@"tokenId=([^&\s]+)[^&]*&token=([^&\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex QueryString();
}
