using Content.Shared._Sunrise.Helpers;
using NUnit.Framework;

namespace Content.Tests.Shared._Sunrise.Helpers;

[TestFixture]
[TestOf(typeof(StringExtensions))]
public sealed class StringExtensionsTests
{
    [TestCase(
        "Р.И.Г. горничной командования",
        9,
        2,
        "Р.И.Г.\nгорнич...")]
    [TestCase(
        "alpha beta gamma",
        10,
        1,
        "alpha b...")]
    [TestCase(
        "alpha beta",
        10,
        2,
        "alpha beta")]
    public void WrapTextAddsEllipsisWhenLaterLinesAreOmitted(
        string text,
        int maxLineLength,
        int maxLines,
        string expected)
    {
        Assert.That(text.WrapText(maxLineLength, maxLines), Is.EqualTo(expected));
    }
}
