namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a self-hosted Umami web analytics container. Its connection string is the base address of Umami, such as
/// <c>http://localhost:3000</c>, which a site receives with <c>WithReference</c> and sends its page views below.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="database">The PostgreSQL database Umami keeps its data in.</param>
/// <param name="appSecretParameter">The parameter that contains Umami's <c>APP_SECRET</c>.</param>
public sealed class UmamiResource(
    [ResourceName] string name,
    PostgresDatabaseResource database,
    ParameterResource appSecretParameter) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>The name of the HTTP endpoint that serves the dashboard and the collection API.</summary>
    public const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>Gets the HTTP endpoint that serves the dashboard and the collection API.</summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, HttpEndpointName);

    /// <summary>Gets the PostgreSQL database Umami keeps its data in; its <see cref="PostgresDatabaseResource.Parent"/> is the server.</summary>
    public PostgresDatabaseResource Database { get; } = database;

    /// <summary>Gets the parameter that contains Umami's <c>APP_SECRET</c>, which signs its login and cache tokens.</summary>
    public ParameterResource AppSecretParameter { get; } = appSecretParameter;

    /// <summary>Gets the base address of Umami, without credentials.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Url)}");
}
