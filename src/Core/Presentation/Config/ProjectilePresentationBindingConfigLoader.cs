using System;
using System.Text.Json.Nodes;
using Ludots.Core.Config;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Projectiles;

namespace Ludots.Core.Presentation.Config
{
    public sealed class ProjectilePresentationBindingConfigLoader
    {
        private readonly ConfigPipeline _pipeline;
        private readonly ProjectilePresentationBindingRegistry _registry;
        private readonly Func<string, int> _resolveImpactEffectId;
        private readonly Func<string, int> _resolvePerformerId;

        public ProjectilePresentationBindingConfigLoader(
            ConfigPipeline pipeline,
            ProjectilePresentationBindingRegistry registry,
            Func<string, int> resolveImpactEffectId,
            Func<string, int> resolvePerformerId)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _resolveImpactEffectId = resolveImpactEffectId ?? throw new ArgumentNullException(nameof(resolveImpactEffectId));
            _resolvePerformerId = resolvePerformerId ?? throw new ArgumentNullException(nameof(resolvePerformerId));
        }

        public void Load(
            ConfigCatalog? catalog = null,
            ConfigConflictReport? report = null,
            string relativePath = "Presentation/projectile_cues.json")
        {
            _registry.Clear();

            var entry = ConfigPipeline.GetEntryOrDefault(catalog, relativePath, ConfigMergePolicy.ArrayById, "id");
            var merged = _pipeline.MergeArrayByIdFromCatalog(in entry, report);
            for (int i = 0; i < merged.Count; i++)
            {
                int impactEffectTemplateId = _resolveImpactEffectId(merged[i].Id);
                if (impactEffectTemplateId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Projectile presentation binding '{merged[i].Id}' in {relativePath} references unknown effect template.");
                }

                var startupPerformers = CompileStartupPerformers(merged[i].Node, merged[i].Id, relativePath);
                _registry.Register(
                    impactEffectTemplateId,
                    new ProjectilePresentationBinding(impactEffectTemplateId, in startupPerformers));
            }
        }

        private PresentationStartupPerformers CompileStartupPerformers(JsonObject node, string bindingId, string relativePath)
        {
            if (node["startupPerformerIds"] is not JsonArray performersNode || performersNode.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Projectile presentation binding '{bindingId}' in {relativePath} requires at least one startupPerformerIds entry.");
            }

            if (performersNode.Count > PresentationStartupPerformers.MaxCount)
            {
                throw new InvalidOperationException(
                    $"Projectile presentation binding '{bindingId}' in {relativePath} exceeds max startup performer count {PresentationStartupPerformers.MaxCount}.");
            }

            var startupPerformers = default(PresentationStartupPerformers);
            startupPerformers.Count = (byte)performersNode.Count;

            for (int i = 0; i < performersNode.Count; i++)
            {
                string performerKey = performersNode[i]?.GetValue<string>() ?? string.Empty;
                int performerId = _resolvePerformerId(performerKey);
                if (performerId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Projectile presentation binding '{bindingId}' in {relativePath} references unknown performer '{performerKey}'.");
                }

                startupPerformers.Set(i, performerId);
            }

            return startupPerformers;
        }
    }
}
