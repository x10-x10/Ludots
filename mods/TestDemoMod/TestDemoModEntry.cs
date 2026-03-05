using Ludots.Core.Modding;
using Ludots.Core.Scripting;

namespace TestDemoMod
{
    public class TestDemoModEntry : IMod
    {
        public void OnLoad(IModContext context)
        {
            context.Log("TestDemoMod Loaded!");
        }

        public void OnUnload()
        {
        }
    }
}