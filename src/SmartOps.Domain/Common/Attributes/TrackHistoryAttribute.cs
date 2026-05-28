namespace SmartOps.Domain.Common.Attributes;

/// <summary>
/// Marks an entity for automatic field-level audit logging.
/// When applied, BaseRepository will capture field diffs on Insert, Update, and SoftDelete.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class TrackHistoryAttribute : Attribute { }
