using System;
using System.Collections.Generic;
using Ludots.Core.Presentation.Components;

namespace Ludots.Core.Presentation.Projectiles
{
    public readonly struct ProjectilePresentationBinding
    {
        public ProjectilePresentationBinding(int impactEffectTemplateId, in PresentationStartupPerformers startupPerformers)
        {
            ImpactEffectTemplateId = impactEffectTemplateId;
            StartupPerformers = startupPerformers;
        }

        public int ImpactEffectTemplateId { get; }
        public PresentationStartupPerformers StartupPerformers { get; }
    }

    public sealed class ProjectilePresentationBindingRegistry
    {
        private readonly Dictionary<int, ProjectilePresentationBinding> _bindings = new();

        public int Count => _bindings.Count;

        public void Clear()
        {
            _bindings.Clear();
        }

        public void Register(int impactEffectTemplateId, in ProjectilePresentationBinding binding)
        {
            if (impactEffectTemplateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactEffectTemplateId), "Projectile presentation bindings require a positive impact effect template id.");
            }

            _bindings[impactEffectTemplateId] = binding;
        }

        public bool TryGet(int impactEffectTemplateId, out ProjectilePresentationBinding binding)
        {
            return _bindings.TryGetValue(impactEffectTemplateId, out binding);
        }
    }
}
