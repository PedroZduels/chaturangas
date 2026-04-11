/// <summary>
/// Maps every FloppyEffectType to its concrete IFloppyEffect implementation.
/// Add a new case here whenever a new effect type is added to FloppyEffectType.
/// </summary>
public static class FloppyEffectFactory
{
    /// <summary>Returns a fresh effect instance for the given type.</summary>
    public static IFloppyEffect Create(FloppyEffectType type)
    {
        return type switch
        {
            FloppyEffectType.Armor      => new ArmorFloppyEffect(),
            FloppyEffectType.Retreat    => new RetreatFloppyEffect(),
            FloppyEffectType.Stun       => new StunFloppyEffect(),
            FloppyEffectType.Upgrade    => new UpgradeFloppyEffect(),
            FloppyEffectType.DoubleJump => new DoubleJumpFloppyEffect(),
            FloppyEffectType.Hack       => new HackFloppyEffect(),
            FloppyEffectType.Ethereal   => new EtherealFloppyEffect(),
            FloppyEffectType.Morph      => new MorphFloppyEffect(),
            FloppyEffectType.Dismantle  => new DismantleFloppyEffect(),
            FloppyEffectType.CtrlZ      => new CtrlZFloppyEffect(),
            FloppyEffectType.Wall       => new WallFloppyEffect(),
            _                           => null
        };
    }
}
