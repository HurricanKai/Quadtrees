#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Toolkit.HighPerformance.Memory;

namespace Quadtree
{
    public readonly struct NaiveHPQuadtree
    {
        public static int CeilToNextPowerOfTwo(int number)
        {
            int a = number;
            int powOfTwo = 1;

            while (a > 1)
            {
                a >>= 1;
                powOfTwo <<= 1;
            }

            if (powOfTwo != number)
            {
                powOfTwo <<= 1;
            }

            return powOfTwo;
        }
        
        public readonly ReadOnlyMemory2D<byte> Data;
        public ReadOnlyMemory<NaiveHPQuadtree>? Leafs => _leafs ?? default;
        private readonly NaiveHPQuadtree[]? _leafs;

        public NaiveHPQuadtree(ReadOnlyMemory2D<byte> data)
        {
            Debug.Assert(CeilToNextPowerOfTwo(data.Height) == data.Height);
            Debug.Assert(CeilToNextPowerOfTwo(data.Width) == data.Width);
            Debug.Assert(data.Width == data.Height);
            
            Data = data;
            if (data.Length == 1)
            {
                _leafs = default;
                return;
            }
            
            var span = data.Span;
            var start = span[0, 0];
            foreach (var val in span)
            {
                if (val == start)
                    continue;
                
                // split
                var halfWidth = Data.Width / 2;
                var halfHeight = Data.Height / 2;
                var width = Data.Width;
                var height = Data.Height;
                _leafs = new[]
                {
                    new NaiveHPQuadtree(data[..halfHeight, ..halfWidth]),
                    new NaiveHPQuadtree(data[..halfHeight, halfWidth..width]),
                    new NaiveHPQuadtree(data[halfHeight..height, ..halfWidth]),
                    new NaiveHPQuadtree(data[halfHeight..height, halfWidth..width]),
                };
                return;
            }

            _leafs = null;
        }
    }
}
