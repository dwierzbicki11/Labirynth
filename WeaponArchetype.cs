public class WeaponArchetype
{
    public string Name { get; init; }
    public int MaxAmmoInMagazine { get; init; }
    public int DefaultSpareMagazines { get; init; }
    public float ReloadTime { get; init; }
    public float BaseRecoil { get; init; }
    public float FireCooldown { get; init; }
    // Docelowo tutaj: public MeshData ModelObj { get; init; }
}