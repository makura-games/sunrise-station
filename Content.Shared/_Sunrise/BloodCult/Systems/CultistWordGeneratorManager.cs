using Robust.Shared.Random;

namespace Content.Shared._Sunrise.BloodCult.Systems;

/// <summary>
/// Words generator for whisper
/// </summary>
public sealed partial class CultistWordGeneratorManager
{
    private const string Vowels = "aeiou";
    private const string Consonants = "bcdfghjklmnpqrstvwxyz";

    [Dependency] private IRobustRandom _random = default!;

    public string GenerateText(string text)
    {
        var content = text.Split(' ');
        var wordsAmount = content.Length;

        if (wordsAmount <= 0)
            return "";

        for (var i = 0; i < wordsAmount; i++)
        {
            content[i] = GenerateWord(content[i].Length) + " ";
        }

        return string.Join("", content);
    }

    private string GenerateWord(int length)
    {
        if (length <= 0)
            throw new ArgumentException("Word length must be greater than zero.");

        var word = "";

        for (var i = 0; i < length; i++)
        {
            var isVowel = (i % 2 == 0); // Alternate between vowels and consonants

            var randomChar = GetRandomChar(isVowel ? Vowels : Consonants);

            word += randomChar;
        }

        return word;
    }

    private string GetRandomChar(string characters)
    {
        var index = _random.Next(characters.Length);
        return characters[index].ToString();
    }
}
