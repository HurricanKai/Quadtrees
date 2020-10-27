using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Toolkit.HighPerformance.Memory;

namespace Quadtree
{
    public class QuadtreeBenchmark
    {
        [Params(4, 8, 1024, 1 << 14)]
        public int N;

        private Memory2D<byte> _mem;

        [GlobalSetup]
        public void GSetup()
        {
            _mem = new byte[N,N];

            var random = new Random();

            _mem.Span.TryGetSpan(out var s);
            random.NextBytes(s);
        }
        
        [Benchmark]
        public NaiveHPQuadtree Naive()
        {
            return new NaiveHPQuadtree(_mem);
        }
        
        [Benchmark]
        public GoodHPQuadtree Good()
        {
            return new GoodHPQuadtree(_mem);
        }
        
        [Benchmark]
        public BetterHPQuadtree Better()
        {
            return new BetterHPQuadtree(_mem);
        }
    }
}
