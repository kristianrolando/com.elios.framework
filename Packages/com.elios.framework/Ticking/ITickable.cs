namespace Elios.Framework.Ticking
{
    // Scaled update. Replaces Update(). deltaTime is Time.deltaTime, so it stops at timeScale 0.
    public interface ITickable
    {
        void Tick(float deltaTime);
    }

    // Unscaled update. Keeps running while the game is paused or slowed.
    // Use for UI, menus, and timers that must ignore Time.timeScale.
    public interface IUnscaledTickable
    {
        void UnscaledTick(float unscaledDeltaTime);
    }

    // Physics update. Replaces FixedUpdate(). Everything that touches a Rigidbody goes here.
    public interface IFixedTickable
    {
        void FixedTick(float fixedDeltaTime);
    }

    // Runs after every scaled tick of the frame. For cameras, anchors, and anything that must
    // read a position only after all movement for the frame is done.
    public interface ILateTickable
    {
        void LateTick(float deltaTime);
    }
}
