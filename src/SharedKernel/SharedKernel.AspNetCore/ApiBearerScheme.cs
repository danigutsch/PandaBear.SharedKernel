namespace PandaBear.AspNetCore;

/// <summary>
/// One named bearer scheme that accepts only tokens issued for exactly <see cref="Audience" /> and maps roles to
/// permissions with its own table, so co-hosted modules never share audiences or permissions.
/// </summary>
/// <param name="Name">The authentication scheme name that route policies select.</param>
/// <param name="Audience">The single audience a token must carry.</param>
/// <param name="PermissionsByRole">The role-to-permission table applied to principals this scheme authenticates.</param>
public sealed record ApiBearerScheme(
    string Name,
    string Audience,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> PermissionsByRole);
