using System.Text;
using System.Text.RegularExpressions;

namespace Content.Shared._Sunrise.Helpers;

/// <summary>
/// Common string helpers used by Sunrise UI and shared text processing.
/// </summary>
public static class StringExtensions
{
    private static readonly Regex AllowedCharsRegex = new Regex(
        @"[^a-zA-Zа-яА-ЯёЁ0-9\s.,!?;:\-_\(\)\[\]{}""'/\\@#%\^&\*\+=<>]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    /// <summary>
    /// Sanitizes user-provided text for UI fields before sending it to the server.
    /// </summary>
    /// <param name="input">Text entered by the user.</param>
    /// <param name="maxLength">Optional maximum length after trimming.</param>
    /// <returns>Trimmed, normalized text without characters outside the allowed set.</returns>
    /// <remarks>
    /// Use this for UI controls such as <c>LineEdit</c> when the resulting text is sent over the network.
    /// </remarks>
    public static string SanitizeInput(this string? input, int? maxLength = null)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        input = input.Trim();

        if (maxLength.HasValue && input.Length > maxLength.Value)
            input = input.Substring(0, maxLength.Value);

        input = input.Normalize(NormalizationForm.FormC);

        input = AllowedCharsRegex.Replace(input, string.Empty);

        return input;
    }

    private const string Ellipsis = "...";

    /// <summary>
    /// Truncates text to a maximum character count and appends an ellipsis when truncation is required.
    /// </summary>
    /// <param name="input">Text to truncate.</param>
    /// <param name="maxLength">Maximum length of the returned text, including the ellipsis.</param>
    /// <returns>
    /// Empty string when <paramref name="input"/> is null or empty, or when <paramref name="maxLength"/> is less than or equal to zero.
    /// Otherwise, the original text or a truncated text ending with an ellipsis.
    /// </returns>
    public static string TruncateWithEllipsis(this string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || maxLength <= 0)
            return string.Empty;

        if (input.Length <= maxLength)
            return input;

        if (maxLength <= Ellipsis.Length)
            return Ellipsis[..maxLength];

        return input[..(maxLength - Ellipsis.Length)] + Ellipsis;
    }

    /// <summary>
    /// Wraps text by character count and limits the result to a maximum number of lines.
    /// </summary>
    /// <param name="text">Text to wrap.</param>
    /// <param name="maxLineLength">Maximum length of each produced line.</param>
    /// <param name="maxLines">Maximum number of lines to return.</param>
    /// <returns>
    /// Text joined with newline separators. If there are more lines than allowed, the last returned line is truncated with an ellipsis.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxLineLength"/> or <paramref name="maxLines"/> is less than or equal to zero.
    /// </exception>
    public static string WrapText(this string text, int maxLineLength, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        var lines = WrapLines(text, maxLineLength);
        if (lines.Count <= maxLines)
            return string.Join('\n', lines);

        lines[maxLines - 1] = TruncateLineWithEllipsis(lines[maxLines - 1], maxLineLength);
        return string.Join('\n', lines.GetRange(0, maxLines));
    }

    /// <summary>
    /// Splits text into lines by character count while preserving whole words where possible.
    /// </summary>
    /// <param name="text">Text to wrap.</param>
    /// <param name="maxLineLength">Maximum length of each produced line.</param>
    /// <returns>
    /// Wrapped lines. Long words are split, preferring a hyphen before the line limit when one is available.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxLineLength"/> is less than or equal to zero.
    /// </exception>
    public static List<string> WrapLines(this string text, int maxLineLength)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return lines;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineLength);

        var currentLine = string.Empty;
        foreach (var rawWord in text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = rawWord.Trim();
            while (word.Length > maxLineLength)
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = string.Empty;
                }

                var splitIndex = GetSplitIndex(word, maxLineLength);
                lines.Add(word[..splitIndex]);
                word = word[splitIndex..];
            }

            if (word.Length == 0)
                continue;

            if (currentLine.Length == 0)
            {
                currentLine = word;
                continue;
            }

            if (currentLine.Length + 1 + word.Length <= maxLineLength)
            {
                currentLine += " " + word;
                continue;
            }

            lines.Add(currentLine);
            currentLine = word;
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine);

        return lines;
    }

    private static int GetSplitIndex(string word, int maxLineLength)
    {
        var hyphenIndex = word.LastIndexOf('-', maxLineLength - 1, maxLineLength);
        if (hyphenIndex > 0)
            return hyphenIndex + 1;

        return maxLineLength;
    }

    private static string TruncateLineWithEllipsis(string line, int maxLineLength)
    {
        if (line.Length + Ellipsis.Length <= maxLineLength)
            return line + Ellipsis;

        if (maxLineLength <= Ellipsis.Length)
            return Ellipsis[..maxLineLength];

        return line[..(maxLineLength - Ellipsis.Length)] + Ellipsis;
    }
}
