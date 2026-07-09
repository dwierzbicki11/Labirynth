public class WeaponInstance
{
    public WeaponArchetype Archetype { get; private set; }
    public int CurrentMagazineAmmo { get; set; }
    public int SpareMagazines { get; set; }

    public WeaponInstance(WeaponArchetype archetype)
    {
        Archetype = archetype;
        CurrentMagazineAmmo = archetype.MaxAmmoInMagazine;
        SpareMagazines = archetype.DefaultSpareMagazines;
    }

    public bool CanShoot() => CurrentMagazineAmmo > 0;
}