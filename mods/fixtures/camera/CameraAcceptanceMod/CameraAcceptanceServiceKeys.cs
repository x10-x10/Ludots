using CameraAcceptanceMod.Runtime;
using Ludots.Core.Scripting;

namespace CameraAcceptanceMod
{
    internal static class CameraAcceptanceServiceKeys
    {
        public static readonly ServiceKey<CameraAcceptanceDiagnosticsState> DiagnosticsState = new("CameraAcceptance.DiagnosticsState");
    }
}
