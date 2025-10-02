using Unity.Entities;
using Unity.Mathematics;

namespace PaintDots.Runtime.Config
{
    /// <summary>
    /// Runtime configuration for chunking behavior.
    /// </summary>
    public readonly struct ChunkConfig : IComponentData
    {
        public readonly bool UseChunking;
        public readonly int2 ChunkSize;

        public ChunkConfig(bool useChunking, int2 chunkSize)
        {
            UseChunking = useChunking;
            ChunkSize = chunkSize;
        }

        public static ChunkConfig CreateDefault()
        {
            return new ChunkConfig(false, new int2(32, 32));
        }
    }
}
