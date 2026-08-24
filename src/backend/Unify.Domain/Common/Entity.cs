namespace Unify.Domain.Common;

/// <summary>
/// Base class for entities that are identified by a stable surrogate key rather than by
/// their attribute values.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Entity id must not be an empty GUID.");
        }

        Id = id;
    }

    public Guid Id { get; }

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other) || (other.GetType() == GetType() && other.Id == Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
