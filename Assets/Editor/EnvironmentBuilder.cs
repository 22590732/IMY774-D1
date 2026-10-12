using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// T15 + T16: one-click set-up of the skybox, the garden beyond glass wall D and the room furniture.
///
///   Tools > Ping Pong > Build Environment (T15 + T16)
///
/// What it does (safe to run again; it replaces what it made last time):
///   1. Texture import settings (normal map, anisotropy, sky panorama without mipmaps).
///   2. URP Lit materials in Assets/Environment/Materials, remapped onto the FBX files by name.
///   3. Skybox/Panoramic material from the CC0 "Spruit Sunrise" panorama, ambient + reflections
///      from the sky, Directional Light turned to match the sun (morning sun in the north-east).
///   4. "Environment (T15 T16)" root in D1 at the House_Shell position, holding Outside.fbx
///      (static, no colliders) and Furniture.fbx (static, non-convex MeshColliders except rugs).
///   5. Grabbable prop prefabs (kettle, toaster, fruit bowl, books, pillows) set up like WoodBoard
///      (its XRGrabInteractable values are copied), placed on the counter, island, tables and bed.
///   6. Box-projected reflection probes per room, glass shadows off, then Generate Lighting.
///
/// All geometry was modelled in house coordinates (house shell guide), so it only needs the same
/// root position as House_Shell. The axis mapping is checked against a marker empty.
/// </summary>
public static class EnvironmentBuilder
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string EnvDir = "Assets/Environment";
    const string MatDir = EnvDir + "/Materials";
    const string TexDir = EnvDir + "/Textures";
    const string ModelDir = EnvDir + "/Models";
    const string SkyTexPath = EnvDir + "/Sky/Sky_SpruitSunrise_4k.jpg";
    const string PrefabDir = "Assets/Prefabs/Props";
    const string TemplatePropPath = "Assets/Prefabs/Props/WoodBoard.prefab";
    const string PhysicsMatPath = "Assets/Physics/CubePhysics.physicMaterial";
    const string RootName = "Environment (T15 T16)";

    // House_Shell sits here in D1; house (x east, y north, z up) -> Unity (-x, z, -y) under it.
    static readonly Vector3 HouseOrigin = new Vector3(2.30416f, 0f, -1.28024f);

    // Sun: compass bearing from north (degrees, clockwise) and elevation for the Directional Light.
    // 60 = ENE: in Pretoria/Johannesburg the morning sun shines into a north-facing garden.
    const float SunBearing = 60f;
    const float SunElevation = 35f;
    // Horizontal position (0..1) of the sun in the panorama image, measured from the JPG.
    const float SunU = 0.6056f;

    static Vector3 House(float x, float y, float z) => HouseOrigin + new Vector3(-x, z, -y);

    // ------------------------------------------------------------------ material table
    struct MatSpec
    {
        public string tex; public float tile; public Color color; public float metallic, smooth;
        public float alpha; public Color emission; public string normal; public float normalScale;
    }

    static MatSpec M(Color c, float smooth, float metallic = 0f, string tex = null, float tile = 1f) =>
        new MatSpec { color = c, smooth = smooth, metallic = metallic, tex = tex, tile = tile, alpha = 1f };

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    static Dictionary<string, MatSpec> Materials()
    {
        var w = Color.white;
        var d = new Dictionary<string, MatSpec>
        {
            // outside (T15) - UVs are in metres, tile = metres per texture repeat
            ["M_Out_Concrete"] = M(w, 0.25f, 0, "T_Concrete", 2f),
            ["M_Out_Grass"] = M(w, 0.08f, 0, "T_Grass", 2f),
            ["M_Out_Field"] = M(w, 0.05f, 0, "T_Field", 8f),
            ["M_Out_Plaster"] = M(w, 0.15f, 0, "T_Plaster", 2f),
            ["M_Out_Coping"] = M(w, 0.30f, 0, "T_Coping", 1.2f),
            ["M_Out_PoolTile"] = M(w, 0.75f, 0, "T_PoolTile", 0.5f),
            ["M_Out_Wood"] = M(C(0.85f, 0.75f, 0.65f), 0.35f, 0, "T_Wood_Oak", 1f),
            ["M_Out_Cushion"] = M(C(0.92f, 0.91f, 0.87f), 0.10f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Out_Metal"] = M(C(0.25f, 0.25f, 0.26f), 0.50f, 0.8f),
            ["M_Out_Canvas"] = M(C(0.90f, 0.86f, 0.78f), 0.10f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Out_Bark"] = M(w, 0.10f, 0, "T_Bark", 1f),
            ["M_Out_Leaves"] = M(w, 0.20f, 0, "T_Leaves", 1f),
            ["M_Out_Pot"] = M(C(0.62f, 0.34f, 0.22f), 0.20f),
            // furniture (T16)
            ["M_Fur_Cabinet"] = M(C(0.90f, 0.89f, 0.86f), 0.40f),
            ["M_Fur_Worktop"] = M(C(0.80f, 0.80f, 0.80f), 0.70f, 0, "T_Concrete", 1f),
            ["M_Fur_White"] = M(C(0.93f, 0.93f, 0.93f), 0.80f),
            ["M_Fur_Steel"] = M(C(0.75f, 0.75f, 0.76f), 0.65f, 1f),
            ["M_Fur_Chrome"] = M(C(0.90f, 0.90f, 0.92f), 0.92f, 1f),
            ["M_Fur_BlackGlass"] = M(C(0.03f, 0.03f, 0.035f), 0.95f),
            ["M_Fur_Black"] = M(C(0.06f, 0.06f, 0.06f), 0.40f, 0.3f),
            ["M_Fur_Oak"] = M(w, 0.40f, 0, "T_Wood_Oak", 1f),
            ["M_Fur_Walnut"] = M(w, 0.45f, 0, "T_Wood_Walnut", 1f),
            ["M_Fur_Fabric"] = M(C(0.50f, 0.53f, 0.57f), 0.05f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Fur_Accent"] = M(C(0.80f, 0.60f, 0.22f), 0.05f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Fur_Linen"] = M(C(0.95f, 0.94f, 0.91f), 0.05f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Fur_Duvet"] = M(C(0.45f, 0.58f, 0.70f), 0.05f, 0, "T_Fabric_Bed", 0.5f),
            ["M_Fur_Rug"] = M(C(0.85f, 0.76f, 0.64f), 0.02f, 0, "T_Fabric_Bed", 0.3f),
            ["M_Fur_Ceramic"] = M(C(0.96f, 0.96f, 0.95f), 0.90f),
            ["M_Fur_Mirror"] = M(C(0.95f, 0.95f, 0.95f), 0.98f, 1f),
            ["M_Fur_Screen"] = M(C(0.01f, 0.01f, 0.012f), 0.95f),
            ["M_Fur_Leaves"] = M(w, 0.25f, 0, "T_Leaves", 0.6f),
            ["M_Fur_Pot"] = M(C(0.82f, 0.80f, 0.76f), 0.50f),
            // grabbable props
            ["M_Prop_Kettle"] = M(C(0.75f, 0.12f, 0.10f), 0.75f, 0.2f),
            ["M_Prop_Toaster"] = M(C(0.80f, 0.80f, 0.82f), 0.70f, 1f),
            ["M_Prop_Bowl"] = M(C(0.92f, 0.90f, 0.84f), 0.85f),
            ["M_Prop_Apple"] = M(C(0.65f, 0.08f, 0.06f), 0.60f),
            ["M_Prop_Orange"] = M(C(0.95f, 0.50f, 0.08f), 0.35f),
            ["M_Prop_Banana"] = M(C(0.95f, 0.82f, 0.25f), 0.30f),
            ["M_Prop_BookBlue"] = M(C(0.12f, 0.22f, 0.45f), 0.30f),
            ["M_Prop_BookRed"] = M(C(0.55f, 0.10f, 0.10f), 0.30f),
            ["M_Prop_Pages"] = M(C(0.95f, 0.93f, 0.86f), 0.10f),
            ["M_Prop_Pillow"] = M(C(0.96f, 0.95f, 0.93f), 0.05f, 0, "T_Fabric_Bed", 0.3f),
        };
        var shade = M(C(0.97f, 0.93f, 0.82f), 0.10f);
        shade.emission = new Color(1f, 0.82f, 0.55f) * 0.9f;
        d["M_Fur_Shade"] = shade;
        var glass = M(C(0.85f, 0.92f, 0.95f), 0.95f);
        glass.alpha = 0.22f;
        d["M_Fur_Glass"] = glass;
        var water = M(C(0.08f, 0.42f, 0.58f), 0.95f);
        water.alpha = 0.72f;
        water.normal = "N_Water";
        water.normalScale = 0.35f;
        water.tile = 2f;
        d["M_Out_Water"] = water;
        return d;
    }

    // ------------------------------------------------------------------ grabbable props
    struct PropSpec
    {
        public string name; public float mass;
        public (float x, float y, float z, float yaw)[] spots;   // house coordinates, yaw = degrees CCW
    }

    static readonly PropSpec[] Props =
    {
        new PropSpec { name = "Prop_Kettle", mass = 1.0f, spots = new[] { (0.28f, 4.70f, 0.90f, -90f) } },
        new PropSpec { name = "Prop_Toaster", mass = 1.2f, spots = new[] { (0.25f, 3.30f, 0.90f, 90f) } },
        new PropSpec { name = "Prop_FruitBowl", mass = 0.9f, spots = new[] { (2.30f, 2.65f, 0.90f, 0f) } },
        new PropSpec { name = "Prop_Book_A", mass = 0.4f, spots = new[] { (7.42f, 3.80f, 0.42f, 75f), (4.05f, 4.70f, 3.65f, 10f) } },
        new PropSpec { name = "Prop_Book_B", mass = 0.4f, spots = new[] { (7.44f, 3.82f, 0.453f, 98f) } },
        new PropSpec { name = "Prop_Pillow", mass = 0.5f, spots = new[] { (0.37f, 3.40f, 3.44f, 90f), (0.37f, 4.10f, 3.44f, 90f) } },
    };

    static readonly string[] NoColliderPrefixes = { "Fur_Rug", "Fur_BathMat" };

    // ================================================================== entry point
    [MenuItem("Tools/Ping Pong/Build Environment (T15 + T16)")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        ConfigureTextures();

        var mats = BuildMaterials();
        RemapModel(ModelDir + "/Outside.fbx", mats);
        RemapModel(ModelDir + "/Furniture.fbx", mats);
        foreach (var p in Props) RemapModel($"{ModelDir}/Props/{p.name}.fbx", mats);

        SetupSkyAndSun();

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        root.transform.position = HouseOrigin;

        PlaceOutside(root.transform);
        PlaceFurniture(root.transform);
        PlaceProps(root.transform);
        AddReflectionProbes(root.transform);
        HouseGlassShadowsOff();

        // T14: the furniture was rebuilt, so tag its surfaces again (only once audio has been set up)
        if (AudioSetup.IsSetUp) AudioSetup.Apply(false);

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!Lightmapping.isRunning) Lightmapping.BakeAsync();
        Debug.Log("[EnvironmentBuilder] Done. Lighting is generating in the background (bottom-right progress bar).");
    }

    // ================================================================== textures + materials
    static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            bool normal = System.IO.Path.GetFileName(path).StartsWith("N_");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 8;          // ground textures are seen at grazing angles
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(SkyTexPath) is TextureImporter sky)
        {
            sky.textureType = TextureImporterType.Default;
            sky.textureShape = TextureImporterShape.Texture2D;
            sky.mipmapEnabled = false;       // avoids the seam line where the panorama wraps
            sky.wrapModeU = TextureWrapMode.Repeat;
            sky.wrapModeV = TextureWrapMode.Clamp;
            sky.maxTextureSize = 4096;
            sky.textureCompression = TextureImporterCompression.CompressedHQ;
            sky.SaveAndReimport();
        }
        else Debug.LogWarning($"[EnvironmentBuilder] Sky texture missing at {SkyTexPath}");
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(EnvDir, "Materials");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var result = new Dictionary<string, Material>();
        foreach (var kv in Materials())
        {
            string path = $"{MatDir}/{kv.Key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = kv.Key };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = lit;
            var s = kv.Value;
            var col = s.color; col.a = s.alpha;
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", s.smooth);
            m.SetFloat("_Metallic", s.metallic);
            var scale = new Vector2(1f / s.tile, 1f / s.tile);
            if (!string.IsNullOrEmpty(s.tex))
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{s.tex}.png"));
            m.SetTextureScale("_BaseMap", scale);
            if (!string.IsNullOrEmpty(s.normal))
            {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{s.normal}.png"));
                m.SetFloat("_BumpScale", s.normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (s.emission.maxColorComponent > 0f)
            {
                m.SetColor("_EmissionColor", s.emission);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (s.alpha < 1f) MakeTransparent(m);
            EditorUtility.SetDirty(m);
            result[kv.Key] = m;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);      // Transparent
        m.SetFloat("_Blend", 0f);        // Alpha
        m.SetFloat("_AlphaClip", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("ShadowCaster", false);
    }

    static void RemapModel(string path, Dictionary<string, Material> mats)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter mi))
        {
            Debug.LogError($"[EnvironmentBuilder] Model not found: {path}");
            return;
        }
        mi.addCollider = false;            // colliders are added per object below
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        foreach (var kv in mats)
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        mi.SaveAndReimport();
    }

    // ================================================================== sky + sun
    static void SetupSkyAndSun()
    {
        // Direction towards the sun in Unity world space (north = -Z, east = -X).
        float b = SunBearing * Mathf.Deg2Rad, e = SunElevation * Mathf.Deg2Rad;
        var houseDir = new Vector3(Mathf.Sin(b), Mathf.Cos(b), 0f);        // east, north
        var toSunFlat = new Vector3(-houseDir.x, 0f, -houseDir.y);
        var toSun = (toSunFlat * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e)).normalized;

        // Skybox/Panoramic samples u = 0.5 - atan2(z, x) / 2pi on the un-rotated direction, and
        // _Rotation turns the sky about Y; this puts the sun in the image on the light's bearing.
        float dirAngle = Mathf.Atan2(toSunFlat.z, toSunFlat.x) * Mathf.Rad2Deg;
        float imgAngle = (0.5f - SunU) * 360f;
        float rotation = Mathf.Repeat(dirAngle - imgAngle, 360f);

        string skyPath = MatDir + "/M_Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        var shader = Shader.Find("Skybox/Panoramic");
        if (sky == null)
        {
            sky = new Material(shader) { name = "M_Sky" };
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        sky.shader = shader;
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexPath));
        sky.SetFloat("_Mapping", 1f);       // Latitude Longitude Layout
        sky.SetFloat("_ImageType", 0f);     // 360 degrees
        sky.SetFloat("_MirrorOnBack", 0f);
        sky.SetFloat("_Exposure", 1.0f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        sky.SetFloat("_Rotation", rotation);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1.0f;

        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null)
        {
            Undo.RecordObject(sun.transform, "Sun");
            Undo.RecordObject(sun, "Sun");
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }
        DynamicGI.UpdateEnvironment();
        Debug.Log($"[EnvironmentBuilder] Sky rotation {rotation:F1} deg, sun bearing {SunBearing} deg, elevation {SunElevation} deg.");
    }

    // ================================================================== placement
    static GameObject InstantiateModel(string path, Transform parent)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) { Debug.LogError($"[EnvironmentBuilder] Missing {path}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    const StaticEditorFlags StaticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                          StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

    static void CheckMapping(Transform root, string child, Vector3 expected)
    {
        var t = FindDeep(root, child);
        if (t == null) { Debug.LogWarning($"[EnvironmentBuilder] {child} not found for the axis check."); return; }
        float err = Vector3.Distance(t.position, expected);
        if (err < 0.01f) Debug.Log($"[EnvironmentBuilder] Axis check OK on {child} ({err * 1000f:F1} mm).");
        else Debug.LogError($"[EnvironmentBuilder] Axis check FAILED on {child}: at {t.position}, expected {expected}. " +
                            "The FBX axis conversion differs from House_Shell; rotate/mirror the root to match.");
    }

    static void PlaceOutside(Transform root)
    {
        var go = InstantiateModel(ModelDir + "/Outside.fbx", root);
        if (go == null) return;
        CheckMapping(go.transform, "Marker_Origin_Check", House(1f, 2f, 3f));
        var marker = FindDeep(go.transform, "Marker_Origin_Check");
        if (marker != null) marker.gameObject.SetActive(false);

        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticFlags);
            string n = r.gameObject.name;
            bool flat = n == "Out_Field" || n == "Out_Lawn" || n == "Out_Patio" || n == "Out_Pool_Water" || n == "Out_Pool_Shell";
            if (flat) r.shadowCastingMode = ShadowCastingMode.Off;
            if (n == "Out_Pool_Water")
            {
                r.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, (StaticEditorFlags)0);   // material instance scrolls
                if (r.GetComponent<UVScroll>() == null) r.gameObject.AddComponent<UVScroll>();
            }
        }
    }

    static void PlaceFurniture(Transform root)
    {
        var go = InstantiateModel(ModelDir + "/Furniture.fbx", root);
        if (go == null) return;
        CheckMapping(go.transform, "Fur_Fridge", House(0.36f, 1.31f, 0f));
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMatPath);
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var g = mf.gameObject;
            GameObjectUtility.SetStaticEditorFlags(g, StaticFlags);
            bool skip = false;
            foreach (var p in NoColliderPrefixes) skip |= g.name.StartsWith(p);
            if (skip)
            {
                g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                continue;
            }
            var mc = g.GetComponent<MeshCollider>();
            if (mc == null) mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.sharedMaterial = pm;
        }
    }

    static void PlaceProps(Transform root)
    {
        var holder = new GameObject("Grabbable Furniture Props").transform;
        holder.SetParent(root, false);
        holder.localPosition = Vector3.zero;
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePropPath);
        var templateGrab = template != null ? template.GetComponent<XRGrabInteractable>() : null;
        if (templateGrab == null) Debug.LogWarning("[EnvironmentBuilder] WoodBoard template not found; using XRI defaults.");
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMatPath);

        foreach (var spec in Props)
        {
            var prefab = BuildPropPrefab(spec, templateGrab, pm);
            if (prefab == null) continue;
            int i = 0;
            foreach (var s in spec.spots)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                inst.name = spec.spots.Length > 1 ? $"{spec.name.Replace("Prop_", "")} {++i}" : spec.name.Replace("Prop_", "");
                // house yaw is counter-clockwise from above; Unity's Y rotation is clockwise.
                inst.transform.SetPositionAndRotation(House(s.x, s.y, s.z) + Vector3.up * 0.002f,
                    Quaternion.Euler(0f, -s.yaw, 0f) * prefab.transform.rotation);
            }
        }
    }

    static GameObject BuildPropPrefab(PropSpec spec, XRGrabInteractable templateGrab, PhysicsMaterial pm)
    {
        string modelPath = $"{ModelDir}/Props/{spec.name}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogError($"[EnvironmentBuilder] Missing {modelPath}"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.transform.position = Vector3.zero;
        var mf = go.GetComponent<MeshFilter>();
        var bounds = mf.sharedMesh.bounds;   // root-local, the same space as the collider

        var box = go.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        box.sharedMaterial = pm;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = spec.mass;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grip = new GameObject("Attach_Grip").transform;
        grip.SetParent(go.transform, false);
        grip.position = go.transform.TransformPoint(bounds.center);
        grip.rotation = Quaternion.identity;      // upright, like the other props

        var grab = go.AddComponent<XRGrabInteractable>();
        if (templateGrab != null && ComponentUtility.CopyComponent(templateGrab))
            ComponentUtility.PasteComponentValues(grab);
        var so = new SerializedObject(grab);
        so.FindProperty("m_Colliders").ClearArray();
        so.FindProperty("m_AttachTransform").objectReferenceValue = grip;
        var sec = so.FindProperty("m_SecondaryAttachTransform");
        if (sec != null) sec.objectReferenceValue = null;
        var mgr = so.FindProperty("m_InteractionManager");
        if (mgr != null) mgr.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        AudioSetup.ApplyToNewProp(go, spec.name.Replace("Prop_", ""));   // T14: keeps impact sounds on re-runs
        string prefabPath = $"{PrefabDir}/{spec.name.Replace("Prop_", "")}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ================================================================== lighting extras
    static void AddReflectionProbes(Transform root)
    {
        var holder = new GameObject("Reflection Probes").transform;
        holder.SetParent(root, false);
        // centre and size in house coordinates (x, y, z) -> Unity size (x, z, y)
        (string n, Vector3 c, Vector3 s)[] rooms =
        {
            ("Probe Ground Floor", new Vector3(4.5f, 2.5f, 1.35f), new Vector3(9.0f, 5.0f, 2.7f)),
            ("Probe Bedroom", new Vector3(2.8f, 3.75f, 4.15f), new Vector3(5.6f, 2.5f, 2.5f)),
            ("Probe Bathroom", new Vector3(7.3f, 3.75f, 4.15f), new Vector3(3.4f, 2.5f, 2.5f)),
        };
        foreach (var r in rooms)
        {
            var g = new GameObject(r.n);
            g.transform.SetParent(holder, false);
            g.transform.position = House(r.c.x, r.c.y, r.c.z);
            var p = g.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.boxProjection = true;
            p.size = new Vector3(r.s.x, r.s.z, r.s.y);
            p.resolution = 256;
            p.blendDistance = 0.5f;
        }
        // House shell must be ReflectionProbeStatic to show up in the baked probes.
        var shell = GameObject.Find("House_Shell");
        if (shell != null)
            foreach (var mr in shell.GetComponentsInChildren<MeshRenderer>(true))
            {
                var f = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, f | StaticEditorFlags.ReflectionProbeStatic);
            }
    }

    static void HouseGlassShadowsOff()
    {
        // Let the sun through glass wall D, the windows and the glass balustrade.
        var shell = GameObject.Find("House_Shell");
        if (shell == null) return;
        foreach (var mr in shell.GetComponentsInChildren<MeshRenderer>(true))
            if (mr.gameObject.name == "House_Glass" || mr.gameObject.name == "House_Rails")
                mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
