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
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Crypto.Bls;

namespace Nethermind.Evm.Precompiles.Bls.Shamatar
{
    /// <summary>
    /// https://eips.ethereum.org/EIPS/eip-2537
    /// </summary>
    public class G1AddPrecompile : IPrecompile
    {
        public static IPrecompile Instance = new G1AddPrecompile();

        private G1AddPrecompile()
        {
        }

        public Address Address { get; } = Address.FromNumber(10);

        public long BaseGasCost(IReleaseSpec releaseSpec)
        {
            return 600L;
        }

        public long DataGasCost(Span<byte> inputData, IReleaseSpec releaseSpec)
        {
            return 0L;
        }

        public PrecompileResult Run(Span<byte> inputData)
        {
            Span<byte> inputDataSpan = stackalloc byte[4 * BlsExtensions.LenFp];
            inputData.PrepareEthInput(inputDataSpan);

            PrecompileResult result;
            
            Span<byte> output = stackalloc byte[2 * BlsExtensions.LenFp];
            bool success = ShamatarLib.BlsG1Add(inputDataSpan, output);
            if (success)
            {
                result = new PrecompileResult(output.ToArray(), true);
            }
            else
            {
                result = PrecompileResult.Failure;
            }

            return result;
        }
    }
}