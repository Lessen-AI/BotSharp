using System.Text.RegularExpressions;

namespace BotSharp.Plugin.FloxiAI.Providers.Chat;

/// <summary>
/// A tool call a Qwen model wrote into its reply text in its own format
/// (<c>&lt;tool_call&gt;&lt;function=name&gt;&lt;parameter=key&gt;value&lt;/parameter&gt;...</c>), because the backend
/// did not turn it into a structured call.
/// </summary>
/// <remarks>
/// Seen on streamed replies: the backend passed the markup through as content, the phone relay spoke it, and the
/// call it named (a hang-up) never ran. Only parameters that were closed are taken, so a call cut off part way
/// leaves the unfinished parameter out rather than half-filled.
/// </remarks>
public static class TextToolCall
{
    public const string Marker = "<tool_call>";

    private static readonly Regex Call = new(
        @"<tool_call>\s*<function=(?<name>[^>\s]+)>(?<body>.*?)(?:</function>|<tool_call>|$)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Parameter = new(
        @"<parameter=(?<key>[^>\s]+)>(?<value>.*?)</parameter>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The first call written in <paramref name="text"/>: its function name, its arguments as a JSON object, and
    /// the text before it; false when the text holds none.
    /// </summary>
    public static bool TryParse(string? text, out string name, out string arguments, out string textBefore)
    {
        name = arguments = textBefore = string.Empty;
        var start = string.IsNullOrEmpty(text) ? -1 : text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var call = Call.Match(text!, start);
        if (!call.Success)
        {
            return false;
        }

        var args = new JsonObject();
        foreach (Match parameter in Parameter.Matches(call.Groups["body"].Value))
        {
            args[parameter.Groups["key"].Value] = Value(parameter.Groups["value"].Value.Trim());
        }

        name = call.Groups["name"].Value;
        arguments = args.ToJsonString();
        textBefore = text![..start].TrimEnd();
        return true;
    }

    /// <summary>
    /// How much of a reply being streamed can go out now: everything before a call written into it, and before
    /// any ending that may still grow into one ("&lt;", "&lt;tool_"), so markup is never streamed to the client.
    /// </summary>
    public static int StreamableLength(string text)
    {
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start >= 0)
        {
            return start;
        }

        for (var length = Math.Min(Marker.Length - 1, text.Length); length > 0; length--)
        {
            if (string.CompareOrdinal(text, text.Length - length, Marker, 0, length) == 0)
            {
                return text.Length - length;
            }
        }
        return text.Length;
    }

    // Qwen writes an object, an array or a boolean as JSON and everything else as plain text. Numbers stay text: a
    // phone number is one, and a schema that wants a number is better served by the text than by a guess.
    private static JsonNode? Value(string raw)
    {
        if (raw is "true" or "false" || raw.StartsWith('{') || raw.StartsWith('['))
        {
            try
            {
                return JsonNode.Parse(raw) ?? JsonValue.Create(raw);
            }
            catch (JsonException)
            {
                // Not JSON after all: kept as the text it is.
            }
        }
        return JsonValue.Create(raw);
    }
}
