using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.ECS
{
    // Centralized sentinels used across the codebase to avoid nulls and spreading literal invalid entity checks
    public static class EntityConstants
    {
        // Under the hood this is the default Entity value, but code reads EntityConstants.InvalidEntity for clarity
        public static readonly Entity InvalidEntity = default(Entity);
    }

    public static class TileConstants
    {
        public const int InvalidTileId = -1;
        public static readonly int2 InvalidPosition = new int2(-9999, -9999);
    }

    // Lightweight optional Entity wrapper that is Burst-friendly and explicit
    public readonly struct OptionalEntity
    {
        public readonly Entity Value;
        public readonly bool HasValue;

        public OptionalEntity(Entity value)
        {
            Value = value;
            HasValue = true;
        }

        // Internal ctor allowing explicit HasValue setting (used for None)
        public OptionalEntity(Entity value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        public static OptionalEntity None => new OptionalEntity(default(Entity), false);
        public static OptionalEntity Some(Entity e) => new OptionalEntity(e);
    }

    public static class CollectionSentinels
    {
        public static readonly int[] EmptyIntArray = new int[0];
        public static readonly Entity[] EmptyEntityArray = new Entity[0];
    }
}
