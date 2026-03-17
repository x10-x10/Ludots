using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arch.Core;
using Ludots.Core.Input.Config;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Runtime;
using NUnit.Framework;

namespace Ludots.Tests.GAS
{
    [TestFixture]
    public sealed class InputOrderContractTests
    {
        [Test]
        public void HeldStartEnd_DoesNotFallbackToBaseOrderType()
        {
            var (backend, handler) = BuildHandler();
            var accumulator = new AuthoritativeInputAccumulator();
            var snapshot = new FrozenInputActionReader();
            var config = new InputOrderMappingConfig
            {
                Mappings = new List<InputOrderMapping>
                {
                    new()
                    {
                        ActionId = "Attack",
                        Trigger = InputTriggerType.Held,
                        HeldPolicy = HeldPolicy.StartEnd,
                        OrderTypeKey = "beam",
                        SelectionType = OrderSelectionType.None,
                        RequireSelection = false,
                        IsSkillMapping = false,
                    }
                }
            };

            var system = new InputOrderMappingSystem(snapshot, config);
            var orders = new List<Ludots.Core.Gameplay.GAS.Orders.Order>();
            system.SetOrderTypeKeyResolver(key => key == "beam" ? 100 : 0);
            system.SetOrderSubmitHandler((in Ludots.Core.Gameplay.GAS.Orders.Order order) => orders.Add(order));

            using var world = World.Create();
            system.SetLocalPlayer(world.Create(), 1);

            backend.Buttons["<Keyboard>/a"] = true;
            handler.Update();
            accumulator.CaptureVisualFrame(handler);

            backend.Buttons["<Keyboard>/a"] = false;
            handler.Update();
            accumulator.CaptureVisualFrame(handler);

            accumulator.BuildTickSnapshot(snapshot);
            system.Update(0f);

            Assert.That(orders, Is.Empty);
        }

        [Test]
        public void RtsDemo_LocalInputAssets_AreCompleteAndReachable()
        {
            string repoRoot = FindRepoRoot();
            string inputPath = Path.Combine(repoRoot, "mods", "RtsDemoMod", "assets", "Input", "default_input.json");
            string mappingPath = Path.Combine(repoRoot, "mods", "RtsDemoMod", "assets", "Input", "input_order_mappings.json");
            string gamePath = Path.Combine(repoRoot, "mods", "RtsDemoMod", "assets", "game.json");

            Assert.That(File.Exists(inputPath), Is.True, $"Missing RTS input config: {inputPath}");
            Assert.That(File.Exists(mappingPath), Is.True, $"Missing RTS mapping config: {mappingPath}");
            Assert.That(File.Exists(gamePath), Is.True, $"Missing RTS game config: {gamePath}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            var inputConfig = JsonSerializer.Deserialize<InputConfigRoot>(File.ReadAllText(inputPath), jsonOptions);
            Assert.That(inputConfig, Is.Not.Null);
            Assert.That(inputConfig!.Contexts.Exists(context => string.Equals(context.Id, "Rts_Gameplay", StringComparison.Ordinal)),
                Is.True,
                "RtsDemoMod must register its gameplay context explicitly.");

            using var mappingStream = File.OpenRead(mappingPath);
            var mappingConfig = InputOrderMappingLoader.LoadFromStream(mappingStream);
            var actionIds = inputConfig.Actions.Select(action => action.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in mappingConfig.Mappings)
            {
                Assert.That(actionIds.Contains(mapping.ActionId), Is.True, $"RTS mapping action '{mapping.ActionId}' is not declared in default_input.json.");
            }

            Assert.That(mappingConfig.Mappings.Any(mapping => string.Equals(mapping.OrderTypeKey, "moveTo", StringComparison.Ordinal)),
                Is.True,
                "RTS local command path must resolve to an explicit move order.");

            using var gameDoc = JsonDocument.Parse(File.ReadAllText(gamePath));
            var startupContexts = gameDoc.RootElement.GetProperty("startupInputContexts")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ToArray();
            Assert.That(startupContexts, Does.Contain("Rts_Gameplay"));
        }

        [Test]
        public void DoubleTapTrigger_SubmitsOnlyOnSecondPressWithinWindow()
        {
            var (backend, handler) = BuildHandler();
            var config = new InputOrderMappingConfig
            {
                Mappings = new List<InputOrderMapping>
                {
                    new()
                    {
                        ActionId = "Attack",
                        Trigger = InputTriggerType.DoubleTap,
                        DoubleTapWindowSeconds = 0.25f,
                        OrderTypeKey = "dash",
                        SelectionType = OrderSelectionType.None,
                        RequireSelection = false,
                        IsSkillMapping = false
                    }
                }
            };

            var orders = new List<Ludots.Core.Gameplay.GAS.Orders.Order>();
            var system = new InputOrderMappingSystem(handler, config);
            system.SetOrderTypeKeyResolver(key => key == "dash" ? 77 : 0);
            system.SetOrderSubmitHandler((in Ludots.Core.Gameplay.GAS.Orders.Order order) => orders.Add(order));

            using var world = World.Create();
            system.SetLocalPlayer(world.Create(), 1);

            backend.Buttons["<Keyboard>/a"] = true;
            handler.Update();
            system.Update(0.10f);

            backend.Buttons["<Keyboard>/a"] = false;
            handler.Update();
            system.Update(0.05f);

            backend.Buttons["<Keyboard>/a"] = true;
            handler.Update();
            system.Update(0.10f);

            Assert.That(orders.Count, Is.EqualTo(1));
            Assert.That(orders[0].OrderTypeId, Is.EqualTo(77));
        }

        private static (TestInputBackend backend, PlayerInputHandler handler) BuildHandler()
        {
            var backend = new TestInputBackend();
            var config = new InputConfigRoot
            {
                Actions = new List<InputActionDef>
                {
                    new() { Id = "Attack", Type = InputActionType.Button },
                },
                Contexts = new List<InputContextDef>
                {
                    new()
                    {
                        Id = "Gameplay",
                        Priority = 1,
                        Bindings = new List<InputBindingDef>
                        {
                            new() { ActionId = "Attack", Path = "<Keyboard>/a", Processors = new() },
                        }
                    }
                }
            };

            var handler = new PlayerInputHandler(backend, config);
            handler.PushContext("Gameplay");
            return (backend, handler);
        }

        private sealed class TestInputBackend : IInputBackend
        {
            public Dictionary<string, bool> Buttons { get; } = new Dictionary<string, bool>();

            public float GetAxis(string devicePath) => 0f;
            public bool GetButton(string devicePath) => Buttons.TryGetValue(devicePath, out var down) && down;
            public Vector2 GetMousePosition() => Vector2.Zero;
            public float GetMouseWheel() => 0f;
            public void EnableIME(bool enable) { }
            public void SetIMECandidatePosition(int x, int y) { }
            public string GetCharBuffer() => string.Empty;
        }

        private static string FindRepoRoot()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                if (File.Exists(Path.Combine(dir, "src", "Core", "Ludots.Core.csproj")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
