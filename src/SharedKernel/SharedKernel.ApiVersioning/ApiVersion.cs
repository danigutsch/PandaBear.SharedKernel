using System.Globalization;

namespace SharedKernel.ApiVersioning;

/// <summary>
/// Represents a public API contract version.
/// </summary>
public readonly record struct ApiVersion : IComparable<ApiVersion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersion"/> struct.
    /// </summary>
    /// <param name="major">The major contract version.</param>
    /// <param name="minor">The minor contract version.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a version component is negative.</exception>
    public ApiVersion(int major, int minor = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);

        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Gets the major contract version.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor contract version.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the route segment for this version, such as <c>v1</c> or <c>v1.1</c>.
    /// </summary>
    public string RouteSegment => Minor == 0
        ? $"v{Major.ToString(CultureInfo.InvariantCulture)}"
        : $"v{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Parses an API version from text, accepting optional leading <c>v</c> route syntax.
    /// </summary>
    /// <param name="value">The version text.</param>
    /// <returns>The parsed API version.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the value is blank or invalid.</exception>
    public static ApiVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return TryParse(value, out ApiVersion version)
            ? version
            : throw new ArgumentException($"'{value}' is not a valid API version.", nameof(value));
    }

    /// <summary>
    /// Attempts to parse an API version from text, accepting optional leading <c>v</c> route syntax.
    /// </summary>
    /// <param name="value">The version text.</param>
    /// <param name="version">The parsed version when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out ApiVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> text = value.Trim();
        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
        {
            text = text[1..];
        }

        Span<Range> parts = stackalloc Range[3];
        int partCount = text.Split(parts, '.');
        if (partCount is < 1 or > 2)
        {
            return false;
        }

        if (!int.TryParse(text[parts[0]], NumberStyles.None, CultureInfo.InvariantCulture, out int major))
        {
            return false;
        }

        int minor = 0;
        if (partCount == 2 && !int.TryParse(text[parts[1]], NumberStyles.None, CultureInfo.InvariantCulture, out minor))
        {
            return false;
        }

        if (major < 0 || minor < 0)
        {
            return false;
        }

        version = new ApiVersion(major, minor);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(ApiVersion other)
    {
        int majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    /// <summary>
    /// Determines whether one API version is lower than another.
    /// </summary>
    public static bool operator <(ApiVersion left, ApiVersion right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one API version is lower than or equal to another.
    /// </summary>
    public static bool operator <=(ApiVersion left, ApiVersion right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one API version is greater than another.
    /// </summary>
    public static bool operator >(ApiVersion left, ApiVersion right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one API version is greater than or equal to another.
    /// </summary>
    public static bool operator >=(ApiVersion left, ApiVersion right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Returns the canonical contract version text, such as <c>1.0</c>.
    /// </summary>
    /// <returns>The canonical contract version text.</returns>
    public override string ToString()
    {
        return $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";
    }
}
