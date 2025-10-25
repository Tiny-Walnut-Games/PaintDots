using System;
using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.Runtime
{
	/// <summary>
	/// Additional sentinel helpers for entities and collections used across the runtime.
	/// </summary>
	public static class TileConstants
	{
		public const int InvalidTileId = -1;
		public static readonly int2 InvalidPosition = new int2(-9999, -9999);
	}

	public readonly struct OptionalEntity
	{
		public readonly Entity Value;
		public readonly bool HasValue;

		public OptionalEntity(Entity value)
		{
			Value = value;
			HasValue = true;
		}

		private OptionalEntity(Entity value, bool hasValue)
		{
			Value = value;
			HasValue = hasValue;
		}

		public static OptionalEntity None => new OptionalEntity(default, false);
		public static OptionalEntity Some(Entity entity) => new OptionalEntity(entity, true);
	}

	public static class CollectionSentinels
	{
		public static readonly int[] EmptyIntArray = Array.Empty<int>();
		public static readonly Entity[] EmptyEntityArray = Array.Empty<Entity>();
	}
}
