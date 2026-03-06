using System;
using System.Collections.Generic;

namespace Ludots.Core.Gameplay.Camera
{
    public sealed class VirtualCameraRegistry
    {
        private readonly Dictionary<string, VirtualCameraDefinition> _items = new(StringComparer.OrdinalIgnoreCase);

        public void Register(VirtualCameraDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new ArgumentException("Virtual camera id is required.", nameof(definition));

            _items[definition.Id] = definition;
        }

        public VirtualCameraDefinition Get(string id)
        {
            if (!TryGet(id, out var definition))
                throw new InvalidOperationException($"Virtual camera '{id}' is not registered.");

            return definition;
        }

        public bool TryGet(string id, out VirtualCameraDefinition definition)
        {
            return _items.TryGetValue(id ?? string.Empty, out definition!);
        }
    }
}
