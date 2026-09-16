namespace LifeOS.Domain.Common;

    /// <summary>
    /// Base type for domain entities identified by a strongly typed <see cref="Guid"/> id.
    /// </summary>
    public abstract class Entity
    {
        public Guid Id { get; protected init; }

        protected Entity()
        {
            // Parameterless constructor for EF Core materialization.
            // EF Core will set the Id property via field access after construction.
        }

        protected Entity(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Entity id cannot be empty.", nameof(id));
            }

            Id = id;
        }

        public override bool Equals(object? obj)
        {
            return obj is Entity other && other.GetType() == GetType() && other.Id == Id;
        }

        public override int GetHashCode() => HashCode.Combine(GetType(), Id);
    }
