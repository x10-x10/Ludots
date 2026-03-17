using System;
using System.Diagnostics;
using System.Text;
using Ludots.Core.Mathematics.FixedPoint;
using Ludots.Core.Navigation2D.Config;
using Ludots.Core.Navigation2D.FlowField;
using Ludots.Core.Navigation2D.Spatial;
using NUnit.Framework;

namespace Ludots.Tests.Navigation2D
{
    [TestFixture]
    [NonParallelizable]
    public sealed class Navigation2DFlowBenchmarkTests
    {
        private const int CellSizeCm = 100;
        private const int TileSizeCells = 64;
        private const int FlowIterations = 65536;

        [Test]
        public void Benchmark_Navigation2DFlow_LargeSparseWorldBudgetedPropagation()
        {
            RunBenchmark(NavFlowBenchmarkScenario.LargeSparseWorldBudgetedPropagation);
        }

        [Test]
        public void Benchmark_Navigation2DFlow_CorridorIncrementalActivation()
        {
            RunBenchmark(NavFlowBenchmarkScenario.CorridorIncrementalActivation);
        }

        private static void RunBenchmark(NavFlowBenchmarkScenario scenario)
        {
            ScenarioRunConfig settings = GetScenarioRunConfig(scenario);
            var sampleAvgMs = new double[settings.SampleCount];
            var sampleAllocBytes = new long[settings.SampleCount];
            var sampleFrontierProcessed = new double[settings.SampleCount];
            var sampleWindowChecks = new double[settings.SampleCount];
            var sampleSelectedTiles = new double[settings.SampleCount];
            var sampleIncrementalSeeds = new double[settings.SampleCount];
            var sampleFullRebuilds = new double[settings.SampleCount];

            for (int sample = 0; sample < settings.SampleCount; sample++)
            {
                using var harness = CreateHarness(scenario);
                WarmupScenario(harness, scenario, settings.WarmupIterations);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.GetAllocatedBytesForCurrentThread();

                long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
                long totalTicks = 0;
                long totalFrontierProcessed = 0;
                long totalWindowChecks = 0;
                long totalSelectedTiles = 0;
                long totalIncrementalSeeds = 0;
                long totalFullRebuilds = 0;
                int previousFullRebuilds = harness.Flow.InstrumentedFullRebuilds;

                long t0 = Stopwatch.GetTimestamp();
                for (int iteration = 0; iteration < settings.MeasuredIterations; iteration++)
                {
                    harness.ApplyScenarioStep(scenario, settings.WarmupIterations + iteration);
                    totalTicks++;
                    totalFrontierProcessed += harness.Flow.InstrumentedFrontierProcessedFrame;
                    totalWindowChecks += harness.Flow.InstrumentedWindowTileChecksFrame;
                    totalSelectedTiles += harness.Flow.InstrumentedSelectedTilesFrame;
                    totalIncrementalSeeds += harness.Flow.InstrumentedIncrementalSeededTilesFrame;
                    totalFullRebuilds += harness.Flow.InstrumentedFullRebuilds - previousFullRebuilds;
                    previousFullRebuilds = harness.Flow.InstrumentedFullRebuilds;
                }
                double elapsedMs = (Stopwatch.GetTimestamp() - t0) * 1000d / Stopwatch.Frequency;
                long afterAlloc = GC.GetAllocatedBytesForCurrentThread();

                sampleAvgMs[sample] = elapsedMs / Math.Max(1, totalTicks);
                sampleAllocBytes[sample] = afterAlloc - beforeAlloc;
                sampleFrontierProcessed[sample] = totalFrontierProcessed / (double)Math.Max(1, totalTicks);
                sampleWindowChecks[sample] = totalWindowChecks / (double)Math.Max(1, totalTicks);
                sampleSelectedTiles[sample] = totalSelectedTiles / (double)Math.Max(1, totalTicks);
                sampleIncrementalSeeds[sample] = totalIncrementalSeeds / (double)Math.Max(1, totalTicks);
                sampleFullRebuilds[sample] = totalFullRebuilds / (double)Math.Max(1, totalTicks);
            }

            PrintResult(scenario, settings, sampleAvgMs, sampleAllocBytes, sampleFrontierProcessed, sampleWindowChecks, sampleSelectedTiles, sampleIncrementalSeeds, sampleFullRebuilds);
        }

        private static ScenarioRunConfig GetScenarioRunConfig(NavFlowBenchmarkScenario scenario)
        {
            return scenario switch
            {
                NavFlowBenchmarkScenario.CorridorIncrementalActivation => new ScenarioRunConfig(6, 24, 2),
                _ => new ScenarioRunConfig(8, 32, 2),
            };
        }

        private static FlowBenchmarkHarness CreateHarness(NavFlowBenchmarkScenario scenario)
        {
            var surface = new CrowdSurface2D(Fix64.FromInt(CellSizeCm), TileSizeCells, initialTileCapacity: 256);
            var config = CreateFlowConfig(scenario);
            var flow = new CrowdFlow2D(surface, config, initialTileCapacity: 256, initialFrontierCapacity: 4096);
            ConfigureScenario(surface, flow, scenario);
            return new FlowBenchmarkHarness(surface, flow);
        }

        private static Navigation2DFlowStreamingConfig CreateFlowConfig(NavFlowBenchmarkScenario scenario)
        {
            return scenario switch
            {
                NavFlowBenchmarkScenario.CorridorIncrementalActivation => new Navigation2DFlowStreamingConfig
                {
                    Enabled = true,
                    ActivationRadiusTiles = 1,
                    MaxActiveTilesPerFlow = 16,
                    UnloadGraceTicks = 2,
                    MaxPotentialCells = 512f,
                    MaxActivationWindowWidthTiles = 12,
                    MaxActivationWindowHeightTiles = 3,
                    WorldBoundsEnabled = true,
                    WorldMinTileX = -2,
                    WorldMinTileY = -2,
                    WorldMaxTileX = 40,
                    WorldMaxTileY = 2,
                },
                _ => new Navigation2DFlowStreamingConfig
                {
                    Enabled = true,
                    ActivationRadiusTiles = 2,
                    MaxActiveTilesPerFlow = 24,
                    UnloadGraceTicks = 2,
                    MaxPotentialCells = 768f,
                    MaxActivationWindowWidthTiles = 11,
                    MaxActivationWindowHeightTiles = 9,
                    WorldBoundsEnabled = true,
                    WorldMinTileX = -512,
                    WorldMinTileY = -512,
                    WorldMaxTileX = 511,
                    WorldMaxTileY = 511,
                }
            };
        }

        private static void ConfigureScenario(CrowdSurface2D surface, CrowdFlow2D flow, NavFlowBenchmarkScenario scenario)
        {
            switch (scenario)
            {
                case NavFlowBenchmarkScenario.LargeSparseWorldBudgetedPropagation:
                    LoadLine(flow, 0, 16, 0);
                    for (int y = -320; y < -256; y++)
                    {
                        for (int x = 256; x < 320; x++)
                        {
                            flow.OnTileLoaded(PackTile(x, y));
                        }
                    }
                    flow.SetGoalPoint(TileCenterCm(0, 0), Fix64.Zero);
                    break;
                case NavFlowBenchmarkScenario.CorridorIncrementalActivation:
                    LoadLine(flow, 0, 36, 0);
                    flow.SetGoalPoint(TileCenterCm(0, 0), Fix64.Zero);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported flow benchmark scenario: {scenario}");
            }
        }

        private static void WarmupScenario(FlowBenchmarkHarness harness, NavFlowBenchmarkScenario scenario, int warmupIterations)
        {
            for (int tick = 0; tick < warmupIterations; tick++)
            {
                harness.ApplyScenarioStep(scenario, tick);
            }
        }

        private static void PrintResult(
            NavFlowBenchmarkScenario scenario,
            ScenarioRunConfig settings,
            double[] sampleAvgMs,
            long[] sampleAllocBytes,
            double[] sampleFrontierProcessed,
            double[] sampleWindowChecks,
            double[] sampleSelectedTiles,
            double[] sampleIncrementalSeeds,
            double[] sampleFullRebuilds)
        {
            var sortedMs = (double[])sampleAvgMs.Clone();
            Array.Sort(sortedMs);
            var sortedAllocs = (long[])sampleAllocBytes.Clone();
            Array.Sort(sortedAllocs);

            Console.WriteLine($"[Benchmark] CrowdFlow2D / {GetScenarioName(scenario)}");
            Console.WriteLine($"  Warmup Iterations: {settings.WarmupIterations}");
            Console.WriteLine($"  Measured Iterations: {settings.MeasuredIterations}");
            Console.WriteLine($"  Samples: {settings.SampleCount}");
            Console.WriteLine($"  Median Avg Flow Tick: {MedianOfSorted(sortedMs):F4}ms");
            Console.WriteLine($"  Min/Max Avg Flow Tick: {sortedMs[0]:F4}ms / {sortedMs[^1]:F4}ms");
            Console.WriteLine($"  Sample Avg Flow Tick: {FormatSamples(sampleAvgMs)}");
            Console.WriteLine($"  Median AllocatedBytes(CurrentThread): {MedianOfSorted(sortedAllocs)}");
            Console.WriteLine($"  Frontier Processed/Tick: {MedianOfSorted((double[])sampleFrontierProcessed.Clone()):F1}");
            Console.WriteLine($"  Window Tile Checks/Tick: {MedianOfSorted((double[])sampleWindowChecks.Clone()):F1}");
            Console.WriteLine($"  Selected Tiles/Tick: {MedianOfSorted((double[])sampleSelectedTiles.Clone()):F1}");
            Console.WriteLine($"  Incremental Seeds/Tick: {MedianOfSorted((double[])sampleIncrementalSeeds.Clone()):F2}");
            Console.WriteLine($"  Full Rebuilds/Tick: {MedianOfSorted((double[])sampleFullRebuilds.Clone()):F2}");
        }

        private static string GetScenarioName(NavFlowBenchmarkScenario scenario)
        {
            return scenario switch
            {
                NavFlowBenchmarkScenario.LargeSparseWorldBudgetedPropagation => "LargeSparseWorldBudgetedPropagation",
                NavFlowBenchmarkScenario.CorridorIncrementalActivation => "CorridorIncrementalActivation",
                _ => scenario.ToString()
            };
        }

        private static string FormatSamples(double[] samples)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < samples.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(samples[i].ToString("F4"));
                sb.Append("ms");
            }

            return sb.ToString();
        }

        private static double MedianOfSorted(double[] values)
        {
            Array.Sort(values);
            int mid = values.Length / 2;
            return (values.Length & 1) != 0
                ? values[mid]
                : (values[mid - 1] + values[mid]) * 0.5;
        }

        private static long MedianOfSorted(long[] sorted)
        {
            int mid = sorted.Length / 2;
            return (sorted.Length & 1) != 0
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2;
        }

        private static void LoadLine(CrowdFlow2D flow, int startTileX, int endTileX, int tileY)
        {
            for (int tileX = startTileX; tileX <= endTileX; tileX++)
            {
                flow.OnTileLoaded(PackTile(tileX, tileY));
            }
        }

        private static long PackTile(int tileX, int tileY)
        {
            return Nav2DKeyPacking.PackInt2(tileX, tileY);
        }

        private static Fix64Vec2 TileCenterCm(int tileX, int tileY)
        {
            int cellX = tileX * TileSizeCells + (TileSizeCells / 2);
            int cellY = tileY * TileSizeCells + (TileSizeCells / 2);
            return Fix64Vec2.FromInt(cellX * CellSizeCm, cellY * CellSizeCm);
        }

        private enum NavFlowBenchmarkScenario
        {
            LargeSparseWorldBudgetedPropagation,
            CorridorIncrementalActivation,
        }

        private readonly record struct ScenarioRunConfig(int WarmupIterations, int MeasuredIterations, int SampleCount);

        private sealed class FlowBenchmarkHarness : IDisposable
        {
            public readonly CrowdSurface2D Surface;
            public readonly CrowdFlow2D Flow;

            public FlowBenchmarkHarness(CrowdSurface2D surface, CrowdFlow2D flow)
            {
                Surface = surface;
                Flow = flow;
            }

            public void ApplyScenarioStep(NavFlowBenchmarkScenario scenario, int tick)
            {
                Flow.BeginDemandFrame(tick);
                switch (scenario)
                {
                    case NavFlowBenchmarkScenario.LargeSparseWorldBudgetedPropagation:
                    {
                        int tileX = 4 + (tick % 6);
                        Flow.AddDemandPoint(TileCenterCm(tileX, 0));
                        break;
                    }
                    case NavFlowBenchmarkScenario.CorridorIncrementalActivation:
                    {
                        int tileX = 2 + tick;
                        if (tileX > 30)
                        {
                            tileX = 30;
                        }

                        Flow.AddDemandPoint(TileCenterCm(tileX, 0));
                        break;
                    }
                }

                Flow.Step(FlowIterations);
            }

            public void Dispose()
            {
                Flow.Dispose();
            }
        }
    }
}
