#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Toolkit.HighPerformance.Memory;

namespace Quadtree
{
    public readonly struct GoodHPQuadtree
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
        public ReadOnlyMemory<GoodHPQuadtree>? Leafs => _leafs ?? default;
        private readonly GoodHPQuadtree[]? _leafs;

        public GoodHPQuadtree(ReadOnlyMemory2D<byte> data)
        {
            Debug.Assert(CeilToNextPowerOfTwo(data.Height) == data.Height);
            Debug.Assert(CeilToNextPowerOfTwo(data.Width) == data.Width);
            Debug.Assert(data.Width == data.Height);
            
            Data = data;
            if (data.Length == (nint)1)
            {
                _leafs = default;
                return;
            }

            static GoodHPQuadtree[] Split(ReadOnlyMemory2D<byte> data)
            {
                var halfWidth = data.Width / 2;
                var halfHeight = data.Height / 2;
                var width = data.Width;
                var height = data.Height;
                return new[]
                {
                    new GoodHPQuadtree(data[..halfHeight, ..halfWidth]),
                    new GoodHPQuadtree(data[..halfHeight, halfWidth..]),
                    new GoodHPQuadtree(data[halfHeight.., ..halfWidth]),
                    new GoodHPQuadtree(data[halfHeight.., halfWidth..]),
                };
            }
            
            var span = data.Span;
            if (span.TryGetSpan(out var s))
            {
                for (int i = 1; i < s.Length; i++)
                {
                    if (s[i] == s[0]) continue;

                    _leafs = Split(data);
                    return;
                }
            }
            
            var start = span[0, 0];
            for (int r = 0; r < span.Height; r++)
            for (int c = 0; c < span.Width; c++)
            {
                if (span.DangerousGetReferenceAt(r, c) == start)
                    continue;

                _leafs = Split(data);
                return;
            }

            _leafs = null;
        }
    }
}
