using System.Text.Json;

namespace CyberEngine.Core;
public static class SystemConfig
{
    public static bool VSync = false;
    public static float Fov = 75f;
    public static int MasterVolume = 100;
    public static int SfxVolume=100;
    public static float MouseSensitivity=0.0025f;
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory,"engine_config.json");
    private class ConfigDto
    {
        public  bool VSync {get;set;}
        public float Fov {get;set;}
        public int MasterVolume {get;set;}
        public int SfxVolume{get;set;}
        public float MouseSensitivity{get;set;}
    }
    public static void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Save();
            return;
        }
        try
        {
            string json = File.ReadAllText(ConfigPath);
            var dto =JsonSerializer.Deserialize<ConfigDto>(json);
            if (dto != null)
            {
                VSync = dto.VSync;
                Fov = dto.Fov;
                MasterVolume = dto.MasterVolume;
                SfxVolume = dto.SfxVolume;
                MouseSensitivity = dto.MouseSensitivity;
            }
        }
        catch(Exception ex)
        {
            Message.error(ex.Message);
        }
    }
    public static void Save()
    {
        try
        {
            var dto = new ConfigDto
            {
                VSync = VSync,
                Fov=Fov,
                MasterVolume = MasterVolume,
                SfxVolume = SfxVolume,
                MouseSensitivity = MouseSensitivity
            };
            var option = new JsonSerializerOptions {WriteIndented = true};
            string json = JsonSerializer.Serialize(dto,option);
            File.WriteAllText(ConfigPath,json);
        }
        catch(Exception ex)
        {
            Message.error(ex.Message);
        }
    }
}