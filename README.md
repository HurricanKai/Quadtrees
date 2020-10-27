# Quadtrees
``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.572 (2004/?/20H1)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET Core SDK=5.0.100-rc.2.20479.15
  [Host]     : .NET Core 5.0.0 (CoreCLR 5.0.20.47505, CoreFX 5.0.20.47505), X64 RyuJIT
  DefaultJob : .NET Core 5.0.0 (CoreCLR 5.0.20.47505, CoreFX 5.0.20.47505), X64 RyuJIT


```
| Method |    N |             Mean |           Error |          StdDev |
|------- |----- |-----------------:|----------------:|----------------:|
|  **Naive** |    **4** |         **726.4 ns** |         **9.26 ns** |         **8.66 ns** |
|   Good |    4 |         549.4 ns |         5.09 ns |         4.77 ns |
| Better |    4 |         236.7 ns |         2.72 ns |         2.54 ns |
|  **Naive** |    **8** |       **2,922.0 ns** |        **12.42 ns** |        **11.62 ns** |
|   Good |    8 |       2,256.8 ns |        18.75 ns |        16.63 ns |
| Better |    8 |         275.5 ns |         1.74 ns |         1.62 ns |
|  **Naive** | **1024** | **158,821,003.3 ns** | **2,762,897.75 ns** | **2,584,416.27 ns** |
|   Good | 1024 | 148,279,420.0 ns | 2,125,463.60 ns | 1,988,159.97 ns |
| Better | 1024 |      12,989.2 ns |       205.96 ns |       192.65 ns |
