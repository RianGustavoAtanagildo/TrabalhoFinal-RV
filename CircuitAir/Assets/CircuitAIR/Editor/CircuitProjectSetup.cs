using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CircuitAIR.EditorTools
{
    /// <summary>
    /// Configura o projeto Circuit AIR de forma repetível (menu CircuitAIR):
    /// marcadores e biblioteca de imagens, materiais e tema, malha da lupa, cena principal,
    /// configurações do Player (Android/iOS) e plug-ins de XR.
    /// </summary>
    public static class CircuitProjectSetup
    {
        private const string Root = "Assets/CircuitAIR";
        private const string MarkersFolder = Root + "/Marcadores";
        private const string MaterialsFolder = Root + "/Materiais";
        private const string ArFolder = Root + "/AR";
        private const string ScenePath = "Assets/Scenes/CircuitAIR.unity";
        private const string OldScenePath = "Assets/Scenes/SampleScene.unity";
        private const string LupaObjPath = Root + "/Models/Lupa/Lupa_CircuitAIR.obj";
        private const string LupaMeshPath = Root + "/Models/Lupa/Lupa_CircuitAIR_Jogo.asset";
        private const string ThemePath = Root + "/CircuitAIR_Theme.asset";
        private const string QrPath = MarkersFolder + "/CircuitQR.jpg";
        private const string LupaMarkerPath = MarkersFolder + "/LupaMarker.png";
        private const string LupaMarkerBackPath = MarkersFolder + "/LupaMarkerVerso.png";
        private const string LibraryPath = ArFolder + "/CircuitAIR_ImageLibrary.asset";

        /// <summary>Tamanho real do QR Code impresso (lado), em metros.</summary>
        public const float QrSizeMeters = 0.10f;

        /// <summary>Tamanho real do marcador da lupa física impresso (lado), em metros.</summary>
        public const float LupaMarkerSizeMeters = LupaController.MarkerSizeMeters;

        [MenuItem("CircuitAIR/1. Importar recursos do TextMeshPro")]
        public static void ImportTextMeshProEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                Debug.Log("CircuitAIR: recursos do TextMeshPro já estão no projeto.");
                return;
            }
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("CircuitAIR/2. Configurar projeto e cena")]
        public static void SetupFromMenu()
        {
            Debug.Log(SetupAll());
        }

        /// <summary>Executa toda a configuração e devolve um relatório.</summary>
        public static string SetupAll()
        {
            var report = new StringBuilder();
            EnsureFolder(MarkersFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(ArFolder);

            Texture2D qr = SetupQrTexture(report);
            Texture2D lupaMarker = SetupLupaMarkerTexture(LupaMarkerPath, 20260425, report);
            Texture2D lupaMarkerBack = SetupLupaMarkerTexture(LupaMarkerBackPath, 20261125, report);
            XRReferenceImageLibrary library = SetupImageLibrary(qr, lupaMarker, lupaMarkerBack, report);
            CircuitTheme theme = SetupTheme(report);
            SetupScene(theme, library, report);
            SetupPlayerSettings(report);
            SetupArBackground(report);
            SetupXrPlugins(report);
            SetupSimulationEnvironment(lupaMarker, report);
            AssetDatabase.SaveAssets();
            report.AppendLine("Configuração concluída.");
            return report.ToString();
        }

        // ------------------------------------------------------------------
        // Imagem da câmera no fundo (URP)
        // ------------------------------------------------------------------

        /// <summary>
        /// Garante o ARBackgroundRendererFeature em todos os renderizadores URP usados pelos níveis de qualidade.
        /// Sem ele, o celular desenha uma cor lisa no lugar da imagem da câmera (o Android usa o nível "Mobile").
        /// </summary>
        public static void SetupArBackground(StringBuilder report)
        {
            var pipelines = new HashSet<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null)
                pipelines.Add(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++)
            {
                RenderPipelineAsset pipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                if (pipeline != null)
                    pipelines.Add(pipeline);
            }

            foreach (RenderPipelineAsset pipeline in pipelines)
            {
                SerializedProperty list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                if (list == null)
                    continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data)
                        report.AppendLine(EnsureArBackgroundFeature(data));
                }
            }
        }

        private static string EnsureArBackgroundFeature(ScriptableRendererData data)
        {
            foreach (ScriptableRendererFeature existing in data.rendererFeatures)
            {
                if (existing is ARBackgroundRendererFeature)
                {
                    existing.SetActive(true);
                    return "Fundo da câmera: " + data.name + " já desenha a câmera.";
                }
            }

            // Mesmo procedimento do botão "Add Renderer Feature" do inspetor da URP.
            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = nameof(ARBackgroundRendererFeature);
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedProperties();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return "Fundo da câmera: ARBackgroundRendererFeature adicionado em " + data.name + ".";
        }

        // ------------------------------------------------------------------
        // Marcadores e biblioteca de imagens
        // ------------------------------------------------------------------

        private static Texture2D SetupQrTexture(StringBuilder report)
        {
            if (!File.Exists(QrPath) && File.Exists("Assets/CircuitQR.jpg"))
            {
                string error = AssetDatabase.MoveAsset("Assets/CircuitQR.jpg", QrPath);
                report.AppendLine(string.IsNullOrEmpty(error) ? "QR Code movido para " + QrPath : "Erro ao mover QR: " + error);
            }
            ConfigureMarkerImporter(QrPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(QrPath);
        }

        private static Texture2D SetupLupaMarkerTexture(string path, int seed, StringBuilder report)
        {
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, LupaMarkerGenerator.Generate(1024, seed).EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                report.AppendLine("Marcador da lupa física criado em " + path);
            }
            ConfigureMarkerImporter(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ConfigureMarkerImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (changed)
                importer.SaveAndReimport();
        }

        private static XRReferenceImageLibrary SetupImageLibrary(Texture2D qr, Texture2D lupaMarker, Texture2D lupaMarkerBack, StringBuilder report)
        {
            if (!File.Exists(LibraryPath) && File.Exists("Assets/CircuitAIR_ImageLibrary.asset"))
                AssetDatabase.MoveAsset("Assets/CircuitAIR_ImageLibrary.asset", LibraryPath);

            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SetImage(library, "CircuitQR", qr, QrSizeMeters);
            SetImage(library, "LupaMarker", lupaMarker, LupaMarkerSizeMeters);
            SetImage(library, "LupaMarkerVerso", lupaMarkerBack, LupaMarkerSizeMeters);
            EditorUtility.SetDirty(library);
            report.AppendLine($"Biblioteca de imagens: {library.count} imagens (CircuitQR {QrSizeMeters * 100f:0} cm, LupaMarker e LupaMarkerVerso {LupaMarkerSizeMeters * 100f:0} cm).");
            return library;
        }

        private static void SetImage(XRReferenceImageLibrary library, string imageName, Texture2D texture, float size)
        {
            int index = -1;
            for (int i = 0; i < library.count; i++)
            {
                if (library[i].name == imageName)
                    index = i;
            }
            if (index < 0)
            {
                library.Add();
                index = library.count - 1;
                library.SetName(index, imageName);
            }
            library.SetTexture(index, texture, true);
            library.SetSpecifySize(index, true);
            library.SetSize(index, new Vector2(size, size));
        }

        // ------------------------------------------------------------------
        // Materiais, malha da lupa e tema
        // ------------------------------------------------------------------

        private static CircuitTheme SetupTheme(StringBuilder report)
        {
            var theme = AssetDatabase.LoadAssetAtPath<CircuitTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<CircuitTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            const string unlit = "Universal Render Pipeline/Unlit";
            const string lit = "Universal Render Pipeline/Lit";
            theme.Wire = CreateMaterial("Fio", unlit, new Color(0.30f, 0.82f, 1f));
            theme.WireGlow = CreateMaterial("Fio_Brilho", unlit, new Color(0.10f, 0.55f, 1f, 0.35f), transparent: true, additive: true);
            theme.WireHit = CreateMaterial("Fio_Toque", unlit, new Color(1f, 0.22f, 0.18f));
            theme.StartBase = CreateMaterial("Base_Inicio", unlit, new Color(0.25f, 1f, 0.55f));
            theme.FinishBase = CreateMaterial("Base_Chegada", unlit, new Color(0.25f, 0.65f, 1f));
            theme.Flag = CreateMaterial("Bandeira", unlit, Color.white, doubleSided: true, texture: CheckerTexture());
            theme.StartGuide = CreateMaterial("Guia_Inicio", unlit, new Color(0.25f, 1f, 0.55f, 0.85f), transparent: true);
            theme.RejoinMarker = CreateMaterial("Guia_Volta", unlit, new Color(1f, 0.82f, 0.25f, 0.9f), transparent: true);
            theme.LupaMetal = CreateMaterial("Lupa_Metal", lit, new Color(0.86f, 0.88f, 0.92f), metallic: 0.9f, smoothness: 0.75f);
            theme.LupaHandle = CreateMaterial("Lupa_Cabo", lit, new Color(1f, 0.42f, 0.08f), metallic: 0f, smoothness: 0.55f);
            theme.LupaOverlay = CreateMaterial("Lupa_Contorno", unlit, new Color(0.35f, 0.85f, 1f, 0.9f), transparent: true);
            theme.LupaMesh = BuildLupaMesh(report);
            theme.LupaMeshRingRadius = 0.094f;
            theme.LupaMeshTubeRadius = 0.006f;
            EditorUtility.SetDirty(theme);
            report.AppendLine("Tema e materiais prontos em " + MaterialsFolder);
            return theme;
        }

        private static Material CreateMaterial(string materialName, string shaderName, Color color,
            bool transparent = false, bool additive = false, bool doubleSided = false,
            float metallic = -1f, float smoothness = -1f, Texture texture = null)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Shader shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            if (metallic >= 0f)
                material.SetFloat("_Metallic", metallic);
            if (smoothness >= 0f)
                material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", additive ? 2f : 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CheckerTexture()
        {
            string path = MaterialsFolder + "/Bandeira_Xadrez.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(6, 4, TextureFormat.RGBA32, false);
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 6; x++)
                        texture.SetPixel(x, y, (x + y) % 2 == 0 ? Color.black : Color.white);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Converte o modelo Lupa_CircuitAIR.obj (em milímetros) para metros e separa em duas partes:
        /// aro de metal (submalha 0) e cabo (submalha 1). O aro fica no plano XZ, centrado na origem.
        /// </summary>
        private static Mesh BuildLupaMesh(StringBuilder report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LupaObjPath);
            MeshFilter source = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            if (source == null || source.sharedMesh == null)
            {
                report.AppendLine("AVISO: modelo da lupa não encontrado; o jogo usará uma argola simples.");
                return null;
            }

            Mesh original = source.sharedMesh;
            Vector3[] vertices = original.vertices;
            Vector3[] normals = original.normals;
            int[] triangles = original.triangles;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] *= 0.001f;

            var ring = new List<int>(triangles.Length);
            var handle = new List<int>(triangles.Length / 2);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                float centroidZ = (vertices[triangles[t]].z + vertices[triangles[t + 1]].z + vertices[triangles[t + 2]].z) / 3f;
                List<int> target = centroidZ > -0.13f ? ring : handle;
                target.Add(triangles[t]);
                target.Add(triangles[t + 1]);
                target.Add(triangles[t + 2]);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LupaMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = "Lupa_CircuitAIR_Jogo";
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length)
                mesh.normals = normals;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(ring, 0);
            mesh.SetTriangles(handle, 1);
            if (normals == null || normals.Length != vertices.Length)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew)
                AssetDatabase.CreateAsset(mesh, LupaMeshPath);
            else
                EditorUtility.SetDirty(mesh);

            report.AppendLine($"Malha da lupa: {vertices.Length} vértices, aro {ring.Count / 3} triângulos, cabo {handle.Count / 3} triângulos.");
            return mesh;
        }

        // ------------------------------------------------------------------
        // Cena
        // ------------------------------------------------------------------

        private static void SetupScene(CircuitTheme theme, XRReferenceImageLibrary library, StringBuilder report)
        {
            if (!File.Exists(ScenePath) && File.Exists(OldScenePath))
            {
                string error = AssetDatabase.MoveAsset(OldScenePath, ScenePath);
                report.AppendLine(string.IsNullOrEmpty(error) ? "Cena renomeada para " + ScenePath : "Erro ao renomear cena: " + error);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Remove o que ficou da versão antiga (lupa solta na origem e Canvas vazio).
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "LupaPrefab" || root.name == "Canvas" || root.name == "LupaTip")
                {
                    report.AppendLine("Removido da cena: " + root.name);
                    Object.DestroyImmediate(root);
                }
            }

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                report.AppendLine("ERRO: a cena não tem XR Origin.");
                return;
            }

            // Em AR de celular a altura extra da câmera não é usada; zerada, o espaço da sessão AR
            // coincide com o da cena (e o XR Simulation calcula certo a distância até os marcadores).
            origin.CameraYOffset = 0f;
            if (origin.CameraFloorOffsetObject != null)
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;

            Camera camera = origin.Camera;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 30f;

            var imageManager = GetOrAdd<ARTrackedImageManager>(origin.gameObject);
            imageManager.referenceLibrary = library;
            imageManager.requestedMaxNumberOfMovingImages = 2;
            var anchorManager = GetOrAdd<ARAnchorManager>(origin.gameObject);
            var arController = GetOrAdd<CircuitARController>(origin.gameObject);

            // Lupa
            Transform lupaTransform = camera.transform.Find("Lupa");
            if (lupaTransform == null)
            {
                lupaTransform = new GameObject("Lupa").transform;
                lupaTransform.SetParent(camera.transform, false);
            }
            var lupa = GetOrAdd<LupaController>(lupaTransform.gameObject);
            SetRefs(lupa, ("theme", theme), ("arCamera", camera));
            var lupaSerialized = new SerializedObject(lupa);
            lupaSerialized.FindProperty("mode").enumValueIndex = (int)LupaMode.PhysicalMarker;
            lupaSerialized.FindProperty("cameraOffset").vector3Value = LupaController.DefaultCameraOffset;
            lupaSerialized.FindProperty("markerToRingOffset").vector3Value = LupaController.DefaultMarkerOffset;
            lupaSerialized.FindProperty("markerToRingTilt").floatValue = LupaController.DefaultMarkerTilt;
            lupaSerialized.FindProperty("physicalRingRadius").floatValue = LupaController.DefaultPhysicalRadius;
            lupaSerialized.FindProperty("physicalTubeRadius").floatValue = LupaController.DefaultPhysicalTubeRadius;
            lupaSerialized.FindProperty("physicalHalfDepth").floatValue = LupaController.DefaultPhysicalHalfDepth;
            lupaSerialized.ApplyModifiedPropertiesWithoutUndo();
            lupaTransform.localPosition = LupaController.DefaultCameraOffset;

            SetRefs(arController,
                ("trackedImageManager", imageManager),
                ("anchorManager", anchorManager),
                ("theme", theme),
                ("lupa", lupa));

            // Jogo + interface + sons
            GameObject game = FindRoot(scene, "CircuitAIR (Jogo)") ?? new GameObject("CircuitAIR (Jogo)");
            var manager = GetOrAdd<CircuitGameManager>(game.gameObject);
            var ui = GetOrAdd<CircuitUI>(game.gameObject);
            if (game.GetComponent<AudioSource>() == null)
                game.AddComponent<AudioSource>();
            var feedback = GetOrAdd<CircuitFeedback>(game.gameObject);
            SetRefs(manager, ("arController", arController), ("lupa", lupa), ("ui", ui), ("feedback", feedback));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            SetRefs(ui, ("font", font));

            // Luz para o modelo da lupa (material Lit).
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Luz direcional");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.None;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                report.AppendLine("Luz direcional adicionada.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            report.AppendLine("Cena pronta: " + ScenePath + " (única cena do build).");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;
            }
            return null;
        }

        private static void SetRefs(Object target, params (string field, Object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogWarning($"CircuitAIR: campo '{field}' não encontrado em {target.GetType().Name}.");
                    continue;
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Player e XR
        // ------------------------------------------------------------------

        private static void SetupPlayerSettings(StringBuilder report)
        {
            PlayerSettings.productName = "Circuit AIR";
            PlayerSettings.companyName = "Equipe 4 - Ciencia da Computacao";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.equipe4.circuitair");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.equipe4.circuitair");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Recomendado pela validação de XR: evita a câmera "presa" quando o app perde o foco.
            PlayerSettings.runInBackground = true;

            // Android: ARCore exige ARM64 + IL2CPP e Android 7.0+; OpenGL ES 3 funciona em mais aparelhos com URP.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // iOS: ARKit precisa da descrição de uso da câmera.
            PlayerSettings.iOS.cameraUsageDescription = "O Circuit AIR usa a câmera para mostrar o circuito em realidade aumentada sobre o QR Code.";
            PlayerSettings.iOS.requiresPersistentWiFi = false;

            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Required;
            // O jogo não usa a Depth API; como "Required" ela esconderia o app de celulares ARCore sem profundidade.
            arcore.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);

            report.AppendLine("Player: Circuit AIR, com.equipe4.circuitair, retrato, Android ARM64/IL2CPP/GLES3, ARCore obrigatório, câmera iOS descrita.");
        }

        private static void SetupXrPlugins(StringBuilder report)
        {
            if (!EditorBuildSettings.TryGetConfigObject(UnityEngine.XR.Management.XRGeneralSettings.settingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                report.AppendLine("AVISO: configurações de XR Plug-in Management não encontradas.");
                return;
            }

            EnsureLoader(perTarget, BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader", report);
            EnsureLoader(perTarget, BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader", report);
            EnsureLoader(perTarget, BuildTargetGroup.Standalone, "UnityEngine.XR.Simulation.SimulationLoader", report);
            EditorUtility.SetDirty(perTarget);
        }

        private static void EnsureLoader(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, string loader, StringBuilder report)
        {
            if (!perTarget.HasSettingsForBuildTarget(group))
                perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var manager = perTarget.ManagerSettingsForBuildTarget(group);
            bool ok = XRPackageMetadataStore.AssignLoader(manager, loader, group);
            report.AppendLine($"XR {group}: {loader.Substring(loader.LastIndexOf('.') + 1)} {(ok ? "ativo" : "NÃO foi ativado")}.");
        }

        /// <summary>
        /// Ambiente de teste no Editor (XR Simulation): a imagem simulada sobre a mesa usa o mesmo
        /// tamanho do QR Code impresso, para o circuito aparecer na escala real.
        /// </summary>
        private const string SimulatedLupaName = "Lupa simulada (LupaMarker)";

        private static void SetupSimulationEnvironment(Texture2D lupaMarker, StringBuilder report)
        {
            const string environmentPath = "Assets/CircuitAIR_enviroment.prefab";
            if (!File.Exists(environmentPath))
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(environmentPath);
            int changed = 0;
            MonoBehaviour qrImage = null;
            bool hasLupa = false;
            foreach (MonoBehaviour behaviour in contents.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "SimulatedTrackedImage")
                    continue;
                if (behaviour.name == SimulatedLupaName)
                {
                    hasLupa = true;
                    var lupaSerialized = new SerializedObject(behaviour);
                    SerializedProperty lupaSize = lupaSerialized.FindProperty("m_ImagePhysicalSizeMeters");
                    if (lupaSize != null && lupaSize.vector2Value != new Vector2(LupaMarkerSizeMeters, LupaMarkerSizeMeters))
                    {
                        lupaSize.vector2Value = new Vector2(LupaMarkerSizeMeters, LupaMarkerSizeMeters);
                        lupaSerialized.ApplyModifiedPropertiesWithoutUndo();
                        changed++;
                    }
                    continue;
                }
                qrImage = behaviour;
                var serialized = new SerializedObject(behaviour);
                SerializedProperty size = serialized.FindProperty("m_ImagePhysicalSizeMeters");
                if (size != null && size.vector2Value != new Vector2(QrSizeMeters, QrSizeMeters))
                {
                    size.vector2Value = new Vector2(QrSizeMeters, QrSizeMeters);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }
            }

            // Marcador da lupa física para testar o modo "lupa física" no Editor:
            // durante o Play, mova o objeto "Lupa simulada (LupaMarker)" na cena do ambiente simulado.
            if (!hasLupa && qrImage != null && lupaMarker != null)
            {
                GameObject lupa = Object.Instantiate(qrImage.gameObject, qrImage.transform.parent);
                lupa.name = SimulatedLupaName;
                Vector3 qrPosition = qrImage.transform.localPosition;
                lupa.transform.localPosition = qrPosition + new Vector3(0.12f, 0.10f, -0.10f);
                lupa.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
                var serialized = new SerializedObject(lupa.GetComponent(qrImage.GetType()));
                serialized.FindProperty("m_Image").objectReferenceValue = lupaMarker;
                serialized.FindProperty("m_ImagePhysicalSizeMeters").vector2Value = new Vector2(LupaMarkerSizeMeters, LupaMarkerSizeMeters);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed++;
                report.AppendLine("Ambiente de simulação: marcador da lupa física adicionado.");
            }

            if (changed > 0)
                PrefabUtility.SaveAsPrefabAsset(contents, environmentPath);
            PrefabUtility.UnloadPrefabContents(contents);
            report.AppendLine($"Ambiente de simulação: QR Code simulado com {QrSizeMeters * 100f:0} cm ({changed} ajuste(s)).");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
