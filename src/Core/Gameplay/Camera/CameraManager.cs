using System;
using System.Numerics;

namespace Ludots.Core.Gameplay.Camera
{
    /// <summary>
    /// Manages the active camera state and controller.
    /// Acts as the central service for camera logic within the GameSession.
    /// No ECS dependency — follow target position is set externally by systems/triggers.
    /// </summary>
    public class CameraManager
    {
        /// <summary>
        /// The current state of the camera (position, rotation, zoom).
        /// </summary>
        public CameraState State { get; private set; } = new CameraState();

        /// <summary>
        /// The active controller that drives the camera state.
        /// </summary>
        public ICameraController Controller { get; private set; }

        /// <summary>
        /// Optional virtual-camera brain that can drive the camera state before input behaviors run.
        /// </summary>
        public VirtualCameraBrain VirtualCameraBrain { get; private set; }

        /// <summary>
        /// Camera follow mode from the active preset.
        /// </summary>
        public CameraFollowMode FollowMode { get; set; }

        /// <summary>
        /// World position (cm) of the follow target, set externally each frame.
        /// Null means no valid follow target.
        /// </summary>
        public Vector2? FollowTargetPositionCm { get; set; }

        /// <summary>
        /// Sets the active camera controller.
        /// </summary>
        public void SetController(ICameraController controller)
        {
            Controller = controller;
        }

        public void SetVirtualCameraBrain(VirtualCameraBrain brain)
        {
            VirtualCameraBrain = brain ?? throw new ArgumentNullException(nameof(brain));
        }

        /// <summary>
        /// Updates the camera state using the active controller.
        /// Should be called once per frame by the GameSession.
        /// </summary>
        public void Update(float dt)
        {
            if (VirtualCameraBrain != null && VirtualCameraBrain.HasActiveCamera)
            {
                VirtualCameraBrain.ApplyToState(State, FollowTargetPositionCm, dt);
                if (VirtualCameraBrain.AllowsInput && Controller != null)
                {
                    Controller.Update(State, dt);
                    VirtualCameraBrain.CapturePostControllerState(State);
                }
                return;
            }

            if (Controller != null)
            {
                Controller.Update(State, dt);
            }
        }
    }
}
