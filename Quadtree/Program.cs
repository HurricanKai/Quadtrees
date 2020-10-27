using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Running;
using Microsoft.Toolkit.HighPerformance.Memory;

namespace Quadtree
{
    unsafe class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<QuadtreeBenchmark>();
        }
    }
}
