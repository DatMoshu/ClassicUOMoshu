using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class SpeechArguments
{
    private static readonly Dictionary<string, int> NumberWords = new Dictionary<string, int>
    {
        { "zero", 0 }, { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 },
        { "five", 5 }, { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 },
        { "ten", 10 }, { "eleven", 11 }, { "twelve", 12 }, { "thirteen", 13 }, { "fourteen", 14 },
        { "fifteen", 15 }, { "sixteen", 16 }, { "seventeen", 17 }, { "eighteen", 18 }, { "nineteen", 19 },
        { "twenty", 20 }, { "thirty", 30 }, { "forty", 40 }, { "fifty", 50 },
        { "sixty", 60 }, { "seventy", 70 }, { "eighty", 80 }, { "ninety", 90 },
        { "hundred", 100 }, { "thousand", 1000 }, { "million", 1000000 }, { "billion", 1000000000 }
    };

    public static int ParseNumber(string text)
    {
        var words = text.ToLower().Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        int result = 0;
        int current = 0;

        foreach (var word in words)
        {
            if (NumberWords.TryGetValue(word, out int value))
            {
                if (value == 100 || value == 1000 || value == 1000000 || value == 1000000000)
                {
                    if (current == 0)
                    {
                        current = 1;
                    }
                    current *= value;
                }
                else
                {
                    current += value;
                }
            }
            else if (word == "and")
            {
                continue;
            }
            else
            {
                result += current;
                current = 0;
            }
        }

        return result + current;
    }

    public static List<int> ParseArguments(string command)
    {
        var matches = Regex.Matches(command, @"\b(?:zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|million|billion|and|-)+\b", RegexOptions.IgnoreCase);
        return matches.Select(match => ParseNumber(match.Value)).ToList();
    }

    public static string ParseArgumentsWithWords(string command)
    {
        var words = command.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        var currentNumberWords = new List<string>();

        foreach (var word in words)
        {

            if (NumberWords.ContainsKey(word.ToLower()) || word.Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                currentNumberWords.Add(word);
            }
            else
            {
                if (currentNumberWords.Count > 0)
                {
                    result.Add(ParseNumber(string.Join(" ", currentNumberWords)).ToString());
                    currentNumberWords.Clear();
                }

                if (word.ToLower() != "space")
                    result.Add(word);
            }
        }

        if (currentNumberWords.Count > 0)
        {
            result.Add(ParseNumber(string.Join(" ", currentNumberWords)).ToString());
        }

        return string.Join(" ", result);
    }
}
