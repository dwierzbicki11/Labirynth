using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Globalization;
using CyberEngine.Core;

namespace CyberEngine.Graphics;

public static class AssetManager
{
    // Cache: Model ładuje się z dysku tylko raz
    private static readonly Dictionary<string, float[]> _meshRegistry = new();

    public static float[] LoadObj(string filePath, float defaultMatId = 2.0f)
    {
        filePath = Path.GetFullPath(filePath);
        if (_meshRegistry.TryGetValue(filePath, out float[] existingMesh))
            return existingMesh;

        if (!File.Exists(filePath))
        {
            Message.error($"[AssetManager] Brakujący plik geometrii: {filePath}");
            return null;
        }

        List<Vector3> tempPositions = new();
        List<Vector2> tempUVs = new();
        List<Vector3> tempNormals = new();
        
        // Docelowa tablica płaska (9 floatów na wierzchołek - dokładnie pod Twój Renderer.cs)
        List<float> finalVertices = new(); 

        using (StreamReader reader = new StreamReader(filePath))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts[0] == "v")
                    tempPositions.Add(new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture)));
                else if (parts[0] == "vt")
                    tempUVs.Add(new Vector2(float.Parse(parts[1], CultureInfo.InvariantCulture), 1.0f - float.Parse(parts[2], CultureInfo.InvariantCulture))); // Inwersja V dla Vulkana
                else if (parts[0] == "vn")
                    tempNormals.Add(new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture)));
                else if (parts[0] == "f")
                {
                    if (parts.Length < 4) continue;
                    // Fan triangulation: obsługuje zarówno trójkąty, jak i wielokąty OBJ.
                    for (int i = 2; i < parts.Length - 1; i++)
                    {
                        string[] face = new[] { parts[1], parts[i], parts[i + 1] };
                        foreach (string faceVertex in face)
                        {
                        string[] indices = faceVertex.Split('/');
                        
                        // 1. POZYCJA (v) - wymagana
                        int vIdx = ResolveObjIndex(int.Parse(indices[0]), tempPositions.Count);
                        if (vIdx < 0 || vIdx >= tempPositions.Count) continue;
                        Vector3 pos = tempPositions[vIdx];
                        finalVertices.Add(pos.X); finalVertices.Add(pos.Y); finalVertices.Add(pos.Z);
                        
                        // 2. NORMALNA (vn) - opcjonalna, ale silnik oczekuje danych
                        if (indices.Length > 2 && !string.IsNullOrEmpty(indices[2]))
                        {
                            int vnIdx = ResolveObjIndex(int.Parse(indices[2]), tempNormals.Count);
                            Vector3 norm = vnIdx >= 0 && vnIdx < tempNormals.Count ? tempNormals[vnIdx] : Vector3.UnitY;
                            finalVertices.Add(norm.X); finalVertices.Add(norm.Y); finalVertices.Add(norm.Z);
                        }
                        else { finalVertices.Add(0f); finalVertices.Add(1f); finalVertices.Add(0f); }

                        // 3. TEKSTURA (vt) - opcjonalna
                        if (indices.Length > 1 && !string.IsNullOrEmpty(indices[1]))
                        {
                            int vtIdx = ResolveObjIndex(int.Parse(indices[1]), tempUVs.Count);
                            Vector2 uv = vtIdx >= 0 && vtIdx < tempUVs.Count ? tempUVs[vtIdx] : Vector2.Zero;
                            finalVertices.Add(uv.X); finalVertices.Add(uv.Y);
                        }
                        else { finalVertices.Add(0f); finalVertices.Add(0f); }

                        // 4. MATERIAŁ (MatID) - wpisujemy na sztywno lub dziedziczymy
                        finalVertices.Add(defaultMatId);
                        }
                    }
                }
            }
        }


        float[] bakedMesh = finalVertices.ToArray();
        _meshRegistry[filePath] = bakedMesh;
        Message.info($"[AssetManager] Załadowano model: {filePath} ({bakedMesh.Length / 9} wierzchołków)");
        return bakedMesh;
    }

    private static int ResolveObjIndex(int index, int count) => index > 0 ? index - 1 : count + index;
}