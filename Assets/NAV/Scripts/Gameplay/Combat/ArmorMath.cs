namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// How armour reduces a hit (Valheim's curve): against hits much stronger than the armour
    /// it subtracts its value; once the armour reaches half the hit, damage falls off as
    /// damage^2 / (4 * armour) - so armour never makes you immune, but old enemies become
    /// harmless as the gear improves. Both branches meet at armour = damage / 2.
    /// </summary>
    public static class ArmorMath
    {
        public static float Reduce(float damage, float armor)
        {
            if (damage <= 0f || armor <= 0f)
            {
                return damage;
            }

            return armor < damage * 0.5f ? damage - armor : damage * damage / (4f * armor);
        }
    }
}
