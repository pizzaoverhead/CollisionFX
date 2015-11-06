using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CollisionFX
{
    public static class ColourManager
    {
        public class DustBody
        {
            public string Name;
            public List<DustBiome> Biomes;
            public override string ToString() { return Name; }
        }

        public class DustBiome
        {
            public string Name;
            public Color DustColour;
            public override string ToString() { return Name; }
        }

        private static List<DustBody> _dustBodies;

        public static void LoadDustColours()
        {
            _dustBodies = new List<DustBody>();
            ConfigNode config = ConfigNode.Load(CollisionFX.ConfigPath);
            if (config == null)
            {
                Debug.LogError("CollisionFX: Configuration file not found at " + CollisionFX.ConfigPath);
                return;
            }
            foreach (ConfigNode node in config.nodes)
            {
                if (!node.name.Equals("DustBody"))
                    continue;

                DustBody body;
                if (node.HasValue("name"))
                {
                    body = new DustBody
                    {
                        Name = node.GetValue("name"),
                        Biomes = new List<DustBiome>()
                    };
                    Debug.Log("#Body " + body.Name);
                }
                else
                {
                    Debug.LogWarning("CollisionFX: Invalid DustBody definition: \"name\" field is missing.");
                    continue;
                }

                if (!node.HasNode("Biomes"))
                    continue;
                ConfigNode biomeNode = node.GetNode("Biomes");
                foreach (ConfigNode.Value biomeDefinition in biomeNode.values)
                {
                    string colourString = biomeDefinition.value;
                    string[] colourValues = colourString.Split(' ');
                    if (colourValues.Length > 4)
                    {
                        Debug.LogWarning("CollisionFX: Invalid Biome colour definition in body \"" +
                            body.Name + "\": Too many parameters.");
                        continue;
                    }
                    float r, g, b, a;
                    NumberStyles flags = NumberStyles.AllowDecimalPoint;
                    if (!float.TryParse(colourValues[0], flags, CultureInfo.InvariantCulture, out r))
                    {
                        Debug.LogWarning("CollisionFX: Invalid Biome colour definition in body \"" +
                            body.Name + "\": \"\" is not a valid integer.");
                        continue;
                    }
                    if (!float.TryParse(colourValues[1], flags, CultureInfo.InvariantCulture, out g))
                    {
                        Debug.LogWarning("CollisionFX: Invalid Biome colour definition in body \"" +
                            body.Name + "\": \"\" is not a valid integer.");
                        continue;
                    }
                    if (!float.TryParse(colourValues[2], flags, CultureInfo.InvariantCulture, out b))
                    {
                        Debug.LogWarning("CollisionFX: Invalid Biome colour definition in body \"" +
                            body.Name + "\": \"\" is not a valid integer.");
                        continue;
                    }

                    Color c;
                    if (colourValues.Length == 4)
                    {
                        if (!float.TryParse(colourValues[3], flags, CultureInfo.InvariantCulture, out a))
                        {
                            Debug.LogWarning("CollisionFX: Invalid Biome colour definition in body \"" +
                                body.Name + "\": \"\" is not a valid integer.");
                            continue;
                        }
                        c = new Color(r, g, b, a);
                    }
                    else
                        c = new Color(r, g, b);

                    DustBiome biome = new DustBiome
                    {
                        Name = biomeDefinition.name,
                        DustColour = c
                    };
                    body.Biomes.Add(biome);
                }
                _dustBodies.Add(body);
            }
        }

        public static string GetCurrentBiomeName(Vessel vessel)
        {
            CBAttributeMapSO biomeMap = FlightGlobals.currentMainBody.BiomeMap;
            CBAttributeMapSO.MapAttribute mapAttribute = biomeMap.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad);
            return mapAttribute.name;
        }

        static Color genericDustColour = new Color(0.8f, 0.8f, 0.8f, 0.007f); // Grey 210 210 210
        static Color dirtColour = new Color(0.65f, 0.48f, 0.34f, 0.05f); // Brown 165, 122, 88
        static Color lightDirtColour = new Color(0.65f, 0.52f, 0.34f, 0.05f); // Brown 165, 132, 88
        static Color sandColour = new Color(0.80f, 0.68f, 0.47f, 0.05f); // Light brown 203, 173, 119
        static Color snowColour = new Color(0.90f, 0.94f, 1f, 0.05f); // Blue-white 230, 250, 255
        private static DustBody _previousDustBody;
        private static DustBiome _previousDustBiome;

        public static Color GetBiomeColour(Collider c)
        {
            if (_dustBodies == null)
                LoadDustColours();

            string currentBody = FlightGlobals.ActiveVessel.mainBody.name;
            DustBody body = null;
            if (_previousDustBody != null &&
                _previousDustBody.Name.Equals(currentBody, StringComparison.InvariantCultureIgnoreCase))
                body = _previousDustBody;
            else
            {
                _previousDustBiome = null;
                foreach (DustBody db in _dustBodies)
                {
                    if (db.Name.Equals(currentBody, StringComparison.InvariantCultureIgnoreCase))
                    {
                        body = db;
                        _previousDustBody = db;
                        break;
                    }
                }
            }

            if (body == null)
            {
                Debug.LogWarning("CollisionFX: Unable to find dust definition for body \"" + currentBody + "\"; using default.");
                return genericDustColour;
            }

            string currentBiome = IsPQS(c) ? GetCurrentBiomeName(FlightGlobals.ActiveVessel) : "Structure";
            DustBiome biome = null;
            if (_previousDustBiome != null &&
                _previousDustBiome.Name.Equals(currentBiome, StringComparison.InvariantCultureIgnoreCase))
                biome = _previousDustBiome;
            else
            {
                foreach (DustBiome b in body.Biomes)
                {
                    if (b.Name.Equals(currentBiome, StringComparison.InvariantCultureIgnoreCase))
                    {
                        biome = b;
                        _previousDustBiome = b;
                        break;
                    }
                }
            }

            if (biome == null)
            {
                Debug.LogWarning("CollisionFX: Unable to find dust definition for biome \"" + currentBiome +
                    "\" on body \"" + currentBody + "\"; using default.");
                return genericDustColour;
            }

            return biome.DustColour;
        }

        public static bool IsPQS(Collider c)
        {
            if (c == null) return false;
            // Test for PQS: Name in the form "Ab0123456789".
            Int64 n;
            return c.name.Length == 12 && Int64.TryParse(c.name.Substring(2, 10), out n);
        }
    }
}
