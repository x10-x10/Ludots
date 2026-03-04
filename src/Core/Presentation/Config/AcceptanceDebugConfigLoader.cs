using System;
using System.Text.Json;
using Ludots.Core.Config;
using Ludots.Core.Diagnostics;

namespace Ludots.Core.Presentation.Config
{
    public sealed class AcceptanceDebugConfigLoader
    {
        private readonly ConfigPipeline _configs;

        public AcceptanceDebugConfigLoader(ConfigPipeline configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public AcceptanceDebugConfig Load(ConfigCatalog catalog = null, ConfigConflictReport report = null)
        {
            try
            {
                var entry = ConfigPipeline.GetEntryOrDefault(catalog, "Presentation/acceptance_debug.json", ConfigMergePolicy.DeepObject);
                var merged = _configs.MergeDeepObjectFromCatalog(in entry, report);
                if (merged == null)
                {
                    var fallback = new AcceptanceDebugConfig();
                    fallback.Normalize();
                    return fallback;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var parsed = JsonSerializer.Deserialize<AcceptanceDebugConfig>(merged.ToJsonString(), options) ?? new AcceptanceDebugConfig();
                parsed.Normalize();
                return parsed;
            }
            catch (Exception ex)
            {
                Log.Error(in LogChannels.Config, $"Failed to load Presentation/acceptance_debug.json: {ex.Message}");
                var fallback = new AcceptanceDebugConfig();
                fallback.Normalize();
                return fallback;
            }
        }
    }
}
