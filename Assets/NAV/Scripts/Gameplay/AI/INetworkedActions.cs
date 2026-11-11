namespace NAV.Gameplay.AI
{
    /// <summary>
    /// A creature whose actions must be seen the same on every machine (a boss's telegraphed
    /// blows and roars). The owner raises ActionForNetwork; the copies on the other machines
    /// play it with PlayActionAsCopy - animation, sounds and effects only, never damage.
    /// </summary>
    public interface INetworkedActions
    {
        event System.Action<int> ActionForNetwork;

        void PlayActionAsCopy(int action);
    }
}
