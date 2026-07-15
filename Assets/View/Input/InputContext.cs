namespace Aerow.View.Input
{
    /// <summary>
    /// Gameplay contexts only. <c>UI</c> is a modal overlay (see
    /// <see cref="GameInput.PushUiModal"/>/<see cref="GameInput.PopUiModal"/>) and
    /// <c>Global</c> is always-on — neither is a value here. Build/Vehicle/Ship join as
    /// their action maps come online.
    /// </summary>
    public enum InputContext
    {
        OnFoot,
    }
}
