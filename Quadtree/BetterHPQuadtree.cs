#nullable enable
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using Microsoft.Toolkit.HighPerformance.Extensions;
using Microsoft.Toolkit.HighPerformance.Memory;

namespace Quadtree
{
    public unsafe readonly struct BetterHPQuadtree
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
        public ReadOnlyMemory<BetterHPQuadtree>? Leafs => _leafs ?? default;
        private readonly BetterHPQuadtree[]? _leafs;

        public BetterHPQuadtree(ReadOnlyMemory2D<byte> data)
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

            static BetterHPQuadtree[] Split(ReadOnlyMemory2D<byte> data)
            {
                var halfWidth = data.Width / 2;
                var halfHeight = data.Height / 2;
                return new[]
                {
                    new BetterHPQuadtree(data[..halfHeight, ..halfWidth]),
                    new BetterHPQuadtree(data[..halfHeight, halfWidth..]),
                    new BetterHPQuadtree(data[halfHeight.., ..halfWidth]),
                    new BetterHPQuadtree(data[halfHeight.., halfWidth..]),
                };
/*
                static unsafe int GetPitch(ReadOnlyMemory2D<byte> data) 
                    => Unsafe.As<byte, int>(ref Unsafe.Add(ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), sizeof(IntPtr) + sizeof(IntPtr) + sizeof(int) + sizeof(int)));

                static unsafe nint GetOffset(ReadOnlyMemory2D<byte> data) 
                    => Unsafe.As<byte, nint>(ref Unsafe.Add(ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), sizeof(IntPtr)));

                static object GetInstance(ReadOnlyMemory2D<byte> data) 
                    => Unsafe.As<byte, object>(ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data));

                static ReadOnlyMemory2D<byte> S1(ReadOnlyMemory2D<byte> data)
                {
                    int
                        shift = 0,
                        pitch = GetPitch(data) + (data.Width / 2);

                    nint offset = GetOffset(data);
                    
                    return ReadOnlyMemory2D<byte>.DangerousCreate(GetInstance(data),
                        ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), data.Height / 2, data.Width / 2, pitch);
                }
                
                static ReadOnlyMemory2D<byte> S2(ReadOnlyMemory2D<byte> data)
                {
                    int
                        shift = data.Width / 2,
                        pitch = GetPitch(data) + (data.Width - data.Width / 2);

                    nint offset = GetOffset(data) + (shift);
                    
                    return ReadOnlyMemory2D<byte>.DangerousCreate(GetInstance(data),
                        ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), data.Height / 2, data.Width / 2, pitch);
                }
                
                static ReadOnlyMemory2D<byte> S3(ReadOnlyMemory2D<byte> data)
                {
                    int
                        shift = (data.Width + GetPitch(data)) * data.Height / 2,
                        pitch = GetPitch(data) + (data.Width - data.Width / 2);

                    nint offset = GetOffset(data) + (shift);   
                    
                    return ReadOnlyMemory2D<byte>.DangerousCreate(GetInstance(data),
                        ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), data.Height / 2, data.Width / 2, pitch);
                }
                
                static ReadOnlyMemory2D<byte> S4(ReadOnlyMemory2D<byte> data)
                {
                    int
                        shift = (data.Width + GetPitch(data)) * data.Height / 2 + data.Width / 2,
                        pitch = GetPitch(data) + (data.Width - data.Width / 2);

                    nint offset = GetOffset(data) + shift;

                    return ReadOnlyMemory2D<byte>.DangerousCreate(GetInstance(data),
                        ref Unsafe.As<ReadOnlyMemory2D<byte>, byte>(ref data), data.Height / 2, data.Width / 2, pitch);
                }

                return new[]
                {
                    new BetterHPQuadtree(S1(data)),
                    new BetterHPQuadtree(S2(data)),
                    new BetterHPQuadtree(S3(data)),
                    new BetterHPQuadtree(S4(data)),
                };*/
            }
            
            var span = data.Span;
            if (span.TryGetSpan(out var s))
            {
                if (!SpanHelper.AllEqualTo(s.Slice(1), s[0]))
                {
                    _leafs = Split(data);
                }

                _leafs = null;
                return;
            }
            
            var start = span[0, 0];
            var l = 32;
            var width = span.Width;
            if (width < 32)
            {
                l = 16;
                if (width < 16)
                {
                    l = 8;
                }
            }

            Span<byte> x = stackalloc byte[l];
            x.Fill(start);
            for (int r = 0; r < span.Height; r++)
            {
                var rs = span.GetRowSpan(r);
                fixed (byte* p = rs)
                fixed (byte* xp = x)
                {
                    if (!SpanHelper.AllEqualTo(ref Unsafe.AsRef<byte>(p), ref Unsafe.AsRef<byte>(xp), (nuint) width))
                        continue;
                    
                    _leafs = Split(data);
                    return;
                }
            }

            _leafs = null;
        }

        public static class SpanHelper
        {
            public static unsafe bool AllEqualTo(ReadOnlySpan<byte> span, byte value)
            {
                Span<byte> x = stackalloc byte[Vector256<byte>.Count];
                x.Fill(value);
                fixed (byte* p = span)
                fixed (byte* xp = x)
                {
                    return AllEqualTo(ref Unsafe.AsRef<byte>(p), ref Unsafe.AsRef<byte>(xp),
                        (nuint) span.Length);
                }
            }
            
            public static unsafe bool AllEqualTo(ref byte first, ref byte second, nuint length)
            {
                bool result;
                // Use nint for arithmetic to avoid unnecessary 64->32->64 truncations
                if (length >= (nuint) sizeof(nuint))
                {
                    // Conditional jmp foward to favor shorter lengths. (See comment at "Equal:" label)
                    // The longer lengths can make back the time due to branch misprediction
                    // better than shorter lengths.
                    goto Longer;
                }

                // On 32-bit, this will always be true since sizeof(nuint) == 4
                if (length < sizeof(uint))
                {
                    uint differentBits = 0;
                    nuint offset = (length & 2);
                    if (offset != 0)
                    {
                        differentBits = LoadUShort(ref first);
                        differentBits -= LoadUShort(ref second);
                    }

                    if ((length & 1) != 0)
                    {
                        differentBits |= (uint) Unsafe.AddByteOffset(ref first, (IntPtr)(void*)offset) -
                                         (uint) second;
                    }

                    result = (differentBits == 0);
                    goto Result;
                }
                else
                {
                    nuint offset = length - sizeof(uint);
                    uint differentBits = LoadUInt(ref first) - LoadUInt(ref second);
                    differentBits |= LoadUInt(ref first, offset) - LoadUInt(ref second);
                    result = (differentBits == 0);
                    goto Result;
                }

                Longer:
                // Only check that the ref is the same if buffers are large,
                // and hence its worth avoiding doing unnecessary comparisons
                if (!Unsafe.AreSame(ref first, ref second))
                {
                    // C# compiler inverts this test, making the outer goto the conditional jmp.
                    goto Vector;
                }

                // This becomes a conditional jmp foward to not favor it.
                goto Equal;

                Result:
                return result;
                // When the sequence is equal; which is the longest execution, we want it to determine that
                // as fast as possible so we do not want the early outs to be "predicted not taken" branches.
                Equal:
                return true;

                Vector:
                if (Sse2.IsSupported)
                {
                    if (Avx2.IsSupported && length >= (nuint) Vector256<byte>.Count)
                    {
                        Vector256<byte> vecResult;
                        nuint offset = 0;
                        nuint lengthToExamine = length - (nuint) Vector256<byte>.Count;
                        // Unsigned, so it shouldn't have overflowed larger than length (rather than negative)
                        Debug.Assert(lengthToExamine < length);
                        if (lengthToExamine != 0)
                        {
                            do
                            {
                                vecResult = Avx2.CompareEqual(LoadVector256(ref first, offset),
                                    LoadVector256(ref second, 0));
                                if (Avx2.MoveMask(vecResult) != -1)
                                {
                                    goto NotEqual;
                                }

                                offset += (nuint) Vector256<byte>.Count;
                            } while (lengthToExamine > offset);
                        }

                        // Do final compare as Vector256<byte>.Count from end rather than start
                        vecResult = Avx2.CompareEqual(LoadVector256(ref first, lengthToExamine),
                            LoadVector256(ref second, 0));
                        if (Avx2.MoveMask(vecResult) == -1)
                        {
                            // C# compiler inverts this test, making the outer goto the conditional jmp.
                            goto Equal;
                        }

                        // This becomes a conditional jmp foward to not favor it.
                        goto NotEqual;
                    }
                    // Use Vector128.Size as Vector128<byte>.Count doesn't inline at R2R time
                    // https://github.com/dotnet/runtime/issues/32714
                    else if (length >= (nuint)Vector128<byte>.Count)
                    {
                        Vector128<byte> vecResult;
                        nuint offset = 0;
                        nuint lengthToExamine = length - (nuint)Vector128<byte>.Count;
                        // Unsigned, so it shouldn't have overflowed larger than length (rather than negative)
                        Debug.Assert(lengthToExamine < length);
                        if (lengthToExamine != 0)
                        {
                            do
                            {
                                // We use instrincs directly as .Equals calls .AsByte() which doesn't inline at R2R time
                                // https://github.com/dotnet/runtime/issues/32714
                                vecResult = Sse2.CompareEqual(LoadVector128(ref first, offset),
                                    LoadVector128(ref second, 0));
                                if (Sse2.MoveMask(vecResult) != 0xFFFF)
                                {
                                    goto NotEqual;
                                }

                                offset += (nuint)Vector128<byte>.Count;
                            } while (lengthToExamine > offset);
                        }

                        // Do final compare as Vector128<byte>.Count from end rather than start
                        vecResult = Sse2.CompareEqual(LoadVector128(ref first, lengthToExamine),
                            LoadVector128(ref second, 0));
                        if (Sse2.MoveMask(vecResult) == 0xFFFF)
                        {
                            // C# compiler inverts this test, making the outer goto the conditional jmp.
                            goto Equal;
                        }

                        // This becomes a conditional jmp foward to not favor it.
                        goto NotEqual;
                    }
                }
                //else if (AdvSimd.Arm64.IsSupported)
                //{
                //    // This API is not optimized with ARM64 intrinsics because there is not much performance win seen
                //    // when compared to the vectorized implementation below. In addition to comparing the bytes in chunks of
                //    // 16-bytes, the only check that is done is if there is a mismatch and if yes, return false. This check
                //    // done with Vector<T> will generate same code by JIT as that if used ARM64 intrinsic instead.
                //}
                else if (Vector.IsHardwareAccelerated && length >= (nuint) Vector<byte>.Count)
                {
                    nuint offset = 0;
                    nuint lengthToExamine = length - (nuint) Vector<byte>.Count;
                    // Unsigned, so it shouldn't have overflowed larger than length (rather than negative)
                    Debug.Assert(lengthToExamine < length);
                    if (lengthToExamine > 0)
                    {
                        do
                        {
                            if (LoadVector(ref first, offset) != LoadVector(ref second, 0))
                            {
                                goto NotEqual;
                            }

                            offset += (nuint) Vector<byte>.Count;
                        } while (lengthToExamine > offset);
                    }

                    // Do final compare as Vector<byte>.Count from end rather than start
                    if (LoadVector(ref first, lengthToExamine) == LoadVector(ref second, 0))
                    {
                        // C# compiler inverts this test, making the outer goto the conditional jmp.
                        goto Equal;
                    }

                    // This becomes a conditional jmp foward to not favor it.
                    goto NotEqual;
                }

                if (Sse2.IsSupported)
                {
                    Debug.Assert(length <= (nuint) sizeof(nuint) * 2);

                    nuint offset = length - (nuint) sizeof(nuint);
                    nuint differentBits = LoadNUInt(ref first) - LoadNUInt(ref second);
                    differentBits |= LoadNUInt(ref first, offset) - LoadNUInt(ref second, 0);
                    result = (differentBits == 0);
                    goto Result;
                }
                else
                {
                    Debug.Assert(length >= (nuint) sizeof(nuint));
                    {
                        nuint offset = 0;
                        nuint lengthToExamine = length - (nuint) sizeof(nuint);
                        // Unsigned, so it shouldn't have overflowed larger than length (rather than negative)
                        Debug.Assert(lengthToExamine < length);
                        if (lengthToExamine > 0)
                        {
                            do
                            {
                                // Compare unsigned so not do a sign extend mov on 64 bit
                                if (LoadNUInt(ref first, offset) != LoadNUInt(ref second, 0))
                                {
                                    goto NotEqual;
                                }

                                offset += (nuint) sizeof(nuint);
                            } while (lengthToExamine > offset);
                        }

                        // Do final compare as sizeof(nuint) from end rather than start
                        result = (LoadNUInt(ref first, lengthToExamine) == LoadNUInt(ref second, 0));
                        goto Result;
                    }
                }

                // As there are so many true/false exit points the Jit will coalesce them to one location.
                // We want them at the end so the conditional early exit jmps are all jmp forwards so the
                // branch predictor in a uninitialized state will not take them e.g.
                // - loops are conditional jmps backwards and predicted
                // - exceptions are conditional fowards jmps and not predicted
                NotEqual:
                return false;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateFirstFoundByte(ulong match)
            => BitOperations.TrailingZeroCount(match) >> 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateLastFoundByte(ulong match)
            => BitOperations.Log2(match) >> 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort LoadUShort(ref byte start)
            => Unsafe.ReadUnaligned<ushort>(ref start);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LoadUInt(ref byte start)
            => Unsafe.ReadUnaligned<uint>(ref start);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LoadUInt(ref byte start, nuint offset)
            => Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref start, (IntPtr)(void*)offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint LoadNUInt(ref byte start)
            => Unsafe.ReadUnaligned<nuint>(ref start);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint LoadNUInt(ref byte start, nuint offset)
            => Unsafe.ReadUnaligned<nuint>(ref Unsafe.AddByteOffset(ref start, (IntPtr)(void*)offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<byte> LoadVector(ref byte start, nuint offset)
            => Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref start, (IntPtr)(void*)offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> LoadVector128(ref byte start, nuint offset)
            => Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AddByteOffset(ref start, (IntPtr)(void*)offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> LoadVector256(ref byte start, nuint offset)
            => Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AddByteOffset(ref start, (IntPtr)(void*)offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint GetByteVectorSpanLength(nuint offset, int length)
            => (nuint)(uint)((length - (int)offset) & ~(Vector<byte>.Count - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint GetByteVector128SpanLength(nuint offset, int length)
            => (nuint)(uint)((length - (int)offset) & ~(Vector128<byte>.Count - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint GetByteVector256SpanLength(nuint offset, int length)
            => (nuint)(uint)((length - (int)offset) & ~(Vector256<byte>.Count - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe nuint UnalignedCountVector(ref byte searchSpace)
        {
            nint unaligned = (nint)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            return (nuint)((Vector<byte>.Count - unaligned) & (Vector<byte>.Count - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe nuint UnalignedCountVector128(ref byte searchSpace)
        {
            nint unaligned = (nint)Unsafe.AsPointer(ref searchSpace) & (Vector128<byte>.Count - 1);
            return (nuint)(uint)((Vector128<byte>.Count - unaligned) & (Vector128<byte>.Count - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe nuint UnalignedCountVectorFromEnd(ref byte searchSpace, int length)
        {
            nint unaligned = (nint)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            return (nuint)(uint)(((length & (Vector<byte>.Count - 1)) + unaligned) & (Vector<byte>.Count - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryFindFirstMatchedLane(Vector128<byte> mask, Vector128<byte> compareResult, ref int matchedLane)
        {
            Debug.Assert(AdvSimd.Arm64.IsSupported);

            // Find the first lane that is set inside compareResult.
            Vector128<byte> maskedSelectedLanes = AdvSimd.And(compareResult, mask);
            Vector128<byte> pairwiseSelectedLane = AdvSimd.Arm64.AddPairwise(maskedSelectedLanes, maskedSelectedLanes);
            ulong selectedLanes = pairwiseSelectedLane.AsUInt64().ToScalar();
            if (selectedLanes == 0)
            {
                // all lanes are zero, so nothing matched.
                return false;
            }

            // Find the first lane that is set inside compareResult.
            matchedLane = BitOperations.TrailingZeroCount(selectedLanes) >> 2;
            return true;
        }
        }
    }
}
