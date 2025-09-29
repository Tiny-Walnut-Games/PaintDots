using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using PaintDots.ECS;

namespace PaintDots.ECS.Systems
{
    // Blob root for tiles
    public struct TileListBlob
    {
        public BlobArray<Tile> Tiles; // Note: Tile must be blittable
    }

    // Blob root for entity lists
    public struct EntityListBlob
    {
        public BlobArray<Entity> Entities;
    }

    public static class TilemapBlobs
    {
        public static BlobAssetReference<TileListBlob> CreateTileListBlob(Tile[] tiles, Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TileListBlob>();
            var arr = builder.Allocate(ref root.Tiles, tiles.Length);
            for (int i = 0; i < tiles.Length; i++) arr[i] = tiles[i];
            var blob = builder.CreateBlobAssetReference<TileListBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        public static BlobAssetReference<EntityListBlob> CreateEntityListBlob(Entity[] entities, Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<EntityListBlob>();
            var arr = builder.Allocate(ref root.Entities, entities.Length);
            for (int i = 0; i < entities.Length; i++) arr[i] = entities[i];
            var blob = builder.CreateBlobAssetReference<EntityListBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }
}
