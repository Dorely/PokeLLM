namespace PokeLLM.State;

/// <summary>
/// Provides deterministic random number generation for game mechanics
/// Ensures reproducible outcomes when the same seed is used
/// </summary>
public class RandomNumberService
{
    private Random _random;
    private string _currentSeed;

    public string CurrentSeed => _currentSeed;

    public RandomNumberService()
    {
        SetSeed(DateTime.UtcNow.Ticks.ToString());
    }

    /// <summary>
    /// Sets a new seed for the random number generator
    /// </summary>
    public void SetSeed(string seed)
    {
        _currentSeed = seed;
        var hashCode = seed.GetHashCode();
        _random = new Random(hashCode);
    }

    /// <summary>
    /// Returns a random integer within the specified range [minValue, maxValue)
    /// </summary>
    public int Next(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }

    /// <summary>
    /// Returns a random integer from 0 to maxValue (exclusive)
    /// </summary>
    public int Next(int maxValue)
    {
        return _random.Next(maxValue);
    }

    /// <summary>
    /// Returns a random double between 0.0 and 1.0
    /// </summary>
    public double NextDouble()
    {
        return _random.NextDouble();
    }

    /// <summary>
    /// Returns a random boolean value
    /// </summary>
    public bool NextBool()
    {
        return _random.Next(2) == 0;
    }

    /// <summary>
    /// Returns a random element from the given array
    /// </summary>
    public T Choose<T>(T[] items)
    {
        if (items == null || items.Length == 0)
            throw new ArgumentException("Items array cannot be null or empty");

        return items[_random.Next(items.Length)];
    }

    /// <summary>
    /// Returns a random element from the given list
    /// </summary>
    public T Choose<T>(IList<T> items)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("Items list cannot be null or empty");

        return items[_random.Next(items.Count)];
    }

    /// <summary>
    /// Rolls a dice with the specified number of sides
    /// </summary>
    public int RollDice(int sides)
    {
        return _random.Next(1, sides + 1);
    }

    /// <summary>
    /// Rolls multiple dice and returns the sum
    /// </summary>
    public int RollDice(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += RollDice(sides);
        }
        return total;
    }

    /// <summary>
    /// Parses dice notation (e.g., "2d6+3") and returns the result
    /// </summary>
    public int RollDiceString(string diceNotation)
    {
        if (string.IsNullOrWhiteSpace(diceNotation))
            return 0;

        try
        {
            // Handle simple cases like "1d6", "2d10", "1d4+3"
            var parts = diceNotation.ToLowerInvariant().Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var dicePart = parts[0].Trim();
            
            if (!dicePart.Contains('d'))
            {
                // Just a number
                return int.Parse(dicePart);
            }

            var diceComponents = dicePart.Split('d');
            var count = int.Parse(diceComponents[0]);
            var sides = int.Parse(diceComponents[1]);
            
            var result = RollDice(count, sides);
            
            // Add modifiers
            if (parts.Length > 1)
            {
                var modifier = int.Parse(parts[1]);
                if (diceNotation.Contains('-'))
                    result -= modifier;
                else
                    result += modifier;
            }
            
            return Math.Max(0, result);
        }
        catch
        {
            // If parsing fails, return a simple d6 roll
            return RollDice(6);
        }
    }

    /// <summary>
    /// Performs a percentage roll (1-100)
    /// </summary>
    public int PercentageRoll()
    {
        return _random.Next(1, 101);
    }

    /// <summary>
    /// Checks if a percentage roll succeeds against a target percentage
    /// </summary>
    public bool PercentageCheck(int targetPercentage)
    {
        return PercentageRoll() <= targetPercentage;
    }
}