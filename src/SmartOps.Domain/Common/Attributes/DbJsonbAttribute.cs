namespace SmartOps.Domain.Common.Attributes;

/// <summary>
/// Marks a string property stored as PostgreSQL jsonb. INSERT/UPDATE SQL will cast parameters with ::jsonb.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DbJsonbAttribute : Attribute;
