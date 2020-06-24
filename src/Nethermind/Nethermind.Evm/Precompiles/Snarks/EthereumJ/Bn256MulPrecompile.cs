//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Crypto.ZkSnarks.Obsolete;

namespace Nethermind.Evm.Precompiles.Snarks.EthereumJ
{
    /// <summary>
    ///     Code adapted from ethereumJ (https://github.com/ethereum/ethereumj)
    /// </summary>
    [Obsolete("Use Bn256MulPrecompile instead")]
    public class Bn256MulPrecompile : IPrecompile
    {
        public static IPrecompile Instance = new Bn256MulPrecompile();

        private Bn256MulPrecompile()
        {
        }

        public Address Address { get; } = Address.FromNumber(7);

        public long BaseGasCost(IReleaseSpec releaseSpec)
        {
            return releaseSpec.IsEip1108Enabled ? 6000L : 40000L;
        }

        public long DataGasCost(Span<byte> inputData, IReleaseSpec releaseSpec = null)
        {
            return 0L;
        }

        public PrecompileResult Run(Span<byte> inputData)
        {
            Metrics.Bn256MulPrecompile++;
            
            Span<byte> inputDataSpan = stackalloc byte[96];
            
            inputData.PrepareEthInput(inputDataSpan);
            Span<byte> x = inputDataSpan.Slice(0, 32);
            Span<byte> y = inputDataSpan.Slice(32, 32);
            
            Span<byte> s = inputDataSpan.Slice(64, 32);

            Bn128Fp p = Bn128Fp.Create(x, y);
            if (p == null)
            {
                return PrecompileResult.Failure;
            }

            BigInteger sInt = s.ToUnsignedBigInteger();
            Bn128Fp res = p.Mul(sInt).ToEthNotation();

            return new PrecompileResult(EncodeResult(res.X.GetBytes(), res.Y.GetBytes()), true);
        }
        
        private static byte[] EncodeResult(byte[] w1, byte[] w2)
        {
            byte[] result = new byte[64];

            // TODO: do I need to strip leading zeros here? // probably not
            w1.AsSpan().WithoutLeadingZeros().CopyTo(result.AsSpan().Slice(32 - w1.Length, w1.Length));
            w2.AsSpan().WithoutLeadingZeros().CopyTo(result.AsSpan().Slice(64 - w2.Length, w2.Length));
            return result;
        }
    }
}