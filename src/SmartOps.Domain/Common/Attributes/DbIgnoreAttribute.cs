namespace SmartOps.Domain.Common.Attributes;

/// <summary>
/// Marks a property to be excluded from auto-generated INSERT/UPDATE SQL queries.
/// Use this on navigation properties, computed properties, or any field that doesn't map to a database column.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DbIgnoreAttribute : Attribute { }
