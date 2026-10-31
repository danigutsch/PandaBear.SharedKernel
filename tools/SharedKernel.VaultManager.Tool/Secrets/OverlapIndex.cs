namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Locations of findings kept so far, hashed by field path and by fixed-size blocks of character offsets, so finding a
/// kept location that overlaps a new one looks at a few nearby locations instead of every kept one.
/// </summary>
/// <remarks>
/// A location is entered in every block from its first to its last character; an empty location is entered in the block
/// of its offset. Two overlapping locations then share at least one block.
/// </remarks>
internal sealed class OverlapIndex
{
    private const int BlockShift = 8;

    private readonly Dictionary<(string FieldPath, int Block), List<int>> blocks = [];
    private readonly List<SecretLocation> locations = [];

    /// <summary>Whether two locations are in the same field and each starts before the other ends.</summary>
    /// <param name="left">First location.</param>
    /// <param name="right">Second location.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="left"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    public static bool Overlaps(SecretLocation left, SecretLocation right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.FieldPath == right.FieldPath && left.Start < right.End && right.Start < left.End;
    }

    /// <summary>Adds a location. Its position is the number of locations added before it.</summary>
    /// <param name="location">Location to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="location"/> is <see langword="null"/>.</exception>
    public void Add(SecretLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        for (int block = FirstBlock(location); block <= LastBlock(location); block++)
        {
            if (!blocks.TryGetValue((location.FieldPath, block), out List<int>? positions))
            {
                positions = [];
                blocks.Add((location.FieldPath, block), positions);
            }

            positions.Add(locations.Count);
        }

        locations.Add(location);
    }

    /// <summary>Finds an added location that overlaps <paramref name="location"/>.</summary>
    /// <param name="location">Location to test.</param>
    /// <returns>The position of an overlapping location, or -1 when none overlaps.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="location"/> is <see langword="null"/>.</exception>
    public int FindOverlap(SecretLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        for (int block = FirstBlock(location); block <= LastBlock(location); block++)
        {
            if (!blocks.TryGetValue((location.FieldPath, block), out List<int>? positions))
            {
                continue;
            }

            foreach (int position in positions)
            {
                if (Overlaps(locations[position], location))
                {
                    return position;
                }
            }
        }

        return -1;
    }

    private static int FirstBlock(SecretLocation location) => location.Start >> BlockShift;

    private static int LastBlock(SecretLocation location) => Math.Max(location.Start, location.End - 1) >> BlockShift;
}
