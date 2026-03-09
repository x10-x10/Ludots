using Ludots.Core.Modding;
using Ludots.Core.Scripting;
using VirtualCameraBaselineMod.Systems;
using VirtualCameraBaselineMod.Triggers;

namespace VirtualCameraBaselineMod
{
    public sealed class VirtualCameraBaselineModEntry : IMod
    {
        public void OnLoad(IModContext context)
        {
            context.Log("[VirtualCameraBaselineMod] Loaded");
            context.OnEvent(GameEvents.GameStart, ctx =>
            {
                var engine = ctx.GetEngine();
                if (engine != null)
                {
                    engine.RegisterPresentationSystem(new VirtualCameraBaselineAcceptanceSystem(engine));
                }

                return System.Threading.Tasks.Task.CompletedTask;
            });
            context.OnEvent(GameEvents.MapLoaded, new VirtualCameraBaselineOnMapLoadedTrigger(context).ExecuteAsync);
        }

        public void OnUnload()
        {
        }
    }
}
