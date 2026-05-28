namespace SmartOps.Domain.Common.Attributes;

/// <summary>
/// Excludes a property from audit history diff capture even when the entity has [TrackHistory].
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TrackHistoryIgnoreAttribute : Attribute { }
