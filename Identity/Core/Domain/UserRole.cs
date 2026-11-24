namespace ChatFusion.Identity.Core.Domain;

/// <summary>
/// Назначение роли пользователю.
/// </summary>
public sealed class UserRole
{
    public Guid UserId { get; set; }

    public short RoleId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>Кто назначил роль. <c>null</c>, если роль назначена системой.</summary>
    public Guid? AssignedBy { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
