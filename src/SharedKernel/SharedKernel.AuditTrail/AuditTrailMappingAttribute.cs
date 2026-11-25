namespace SharedKernel.AuditTrail;

/// <summary>Marks a static method that maps one domain event to one audit-trail entry.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AuditTrailMappingAttribute : Attribute;
