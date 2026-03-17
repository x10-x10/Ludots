using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Ludots.Core.Config;
using Ludots.Core.Engine;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Selection;
using Ludots.Core.Navigation.Pathing;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Scripting;

namespace CoreInputMod.Systems
{
    public sealed class SelectedMovePathPresentationSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;
        private readonly SelectionRuntime _selection;
        private readonly Entity[] _selected = new Entity[SelectionBuffer.CAPACITY];
        private SelectedMovePathOverlayBridge? _bridge;

        public SelectedMovePathPresentationSystem(World world, Dictionary<string, object> globals, SelectionRuntime selection)
        {
            _world = world;
            _globals = globals;
            _selection = selection;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }

        public void Update(in float dt)
        {
            if (_bridge == null && !TryCreateBridge(out _bridge))
            {
                return;
            }

            int count = SelectionViewRuntime.CopyViewedSelection(_world, _globals, _selection, _selected);
            if (count <= 0)
            {
                return;
            }

            _bridge.UpdateViewedSelection(new ReadOnlySpan<Entity>(_selected, 0, count));
        }

        private bool TryCreateBridge(out SelectedMovePathOverlayBridge bridge)
        {
            bridge = default!;
            if (!_globals.TryGetValue(CoreServiceKeys.GameConfig.Name, out var configObj) ||
                configObj is not GameConfig config ||
                !config.Constants.OrderTypeIds.TryGetValue("moveTo", out int moveToOrderTypeId) ||
                moveToOrderTypeId <= 0 ||
                !_globals.TryGetValue(CoreServiceKeys.PathService.Name, out var pathServiceObj) ||
                pathServiceObj is not IPathService pathService ||
                !_globals.TryGetValue(CoreServiceKeys.PathStore.Name, out var pathStoreObj) ||
                pathStoreObj is not PathStore pathStore ||
                !_globals.TryGetValue(CoreServiceKeys.GroundOverlayBuffer.Name, out var overlaysObj) ||
                overlaysObj is not GroundOverlayBuffer overlays)
            {
                return false;
            }

            bridge = new SelectedMovePathOverlayBridge(_world, pathService, pathStore, overlays, moveToOrderTypeId);
            return true;
        }
    }
}
