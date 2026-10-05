using System.Globalization;

namespace Automation.Simulator.TestServer.Simulations;

internal static class ScenarioParser
{
    public static ScenarioDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Scenario file not found.", path);
        }

        string name = Path.GetFileNameWithoutExtension(path);
        var steps = new List<ScenarioStep>();
        ScenarioStep? currentStep = null;
        string? currentSection = null;

        foreach (string sourceLine in File.ReadLines(path))
        {
            string line = RemoveComment(sourceLine).Trim();
            if (string.IsNullOrWhiteSpace(line) || line.Equals("scenario:", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("steps:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                name = Unquote(ValueAfterColon(line));
                continue;
            }

            if (line.StartsWith("- afterMs:", StringComparison.OrdinalIgnoreCase))
            {
                currentStep = new ScenarioStep
                {
                    AfterMs = int.Parse(ValueAfterColon(line), CultureInfo.InvariantCulture)
                };
                steps.Add(currentStep);
                currentSection = null;
                continue;
            }

            if (currentStep is null)
            {
                continue;
            }

            if (line.Equals("set:", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("increment:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = line[..^1].ToLowerInvariant();
                continue;
            }

            if (line.StartsWith("activateMalfunction:", StringComparison.OrdinalIgnoreCase))
            {
                currentStep.ActivateMalfunction = Unquote(ValueAfterColon(line));
                currentSection = null;
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0 || currentSection is null)
            {
                continue;
            }

            string key = Unquote(line[..colon].Trim());
            string valueText = line[(colon + 1)..].Trim();
            if (currentSection == "set")
            {
                currentStep.Set[key] = ParseScalar(valueText);
            }
            else if (double.TryParse(
                         valueText,
                         NumberStyles.Float,
                         CultureInfo.InvariantCulture,
                         out double amount))
            {
                currentStep.Increment[key] = amount;
            }
        }

        if (steps.Count == 0)
        {
            throw new InvalidDataException($"Scenario '{path}' contains no steps.");
        }

        return new ScenarioDefinition
        {
            Name = name,
            Steps = steps.OrderBy(step => step.AfterMs).ToList()
        };
    }

    private static object? ParseScalar(string text)
    {
        string value = Unquote(text);
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (bool.TryParse(value, out bool boolean)) return boolean;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return integer;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
        return value;
    }

    private static string ValueAfterColon(string line) =>
        line[(line.IndexOf(':') + 1)..].Trim();

    private static string Unquote(string value) =>
        value.Trim().Trim('"', '\'');

    private static string RemoveComment(string line)
    {
        int index = line.IndexOf('#');
        return index < 0 ? line : line[..index];
    }
}
