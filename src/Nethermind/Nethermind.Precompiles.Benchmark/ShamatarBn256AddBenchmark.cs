﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using Nethermind.Evm.Precompiles;
using Nethermind.Evm.Precompiles.Mcl.Bn256;

namespace Nethermind.Precompiles.Benchmark
{
    [HtmlExporter]
    [MemoryDiagnoser]
    // [NativeMemoryProfiler]
    [ShortRunJob(RuntimeMoniker.NetCoreApp31)]
    // [DryJob(RuntimeMoniker.NetCoreApp31)]
    public class ShamatarBn256AddBenchmark : PrecompileBenchmarkBase
    {
        protected override IPrecompile Precompile => ShamatarBn256AddPrecompile.Instance;
        protected override string InputsDirectory => "bnadd";
    }
}