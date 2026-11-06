using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace ProcurementHero.EditorTools
{
    /// <summary>
    /// Tools/Procurement Hero/Setup Game.
    /// 전용 폴더 아래에 데이터 · 폰트 에셋 · 씬을 만들고 참조를 연결한다.
    /// 여러 번 실행해도 중복을 만들지 않고, 이미 있는 데이터 에셋은 덮어쓰지 않는다.
    /// </summary>
    public static class GameSetup
    {
        public const string Root = "Assets/ProcurementHero";
        public const string DataFolder = Root + "/Data";
        public const string SceneFolder = Root + "/Scenes";
        public const string FontFolder = Root + "/Fonts";
        public const string ScenePath = SceneFolder + "/ProcurementHero.unity";
        public const string DatabasePath = DataFolder + "/GameDatabase.asset";
        public const string FontAssetPath = FontFolder + "/Pretendard SDF.asset";
        public const string BoldFontAssetPath = FontFolder + "/Pretendard Bold SDF.asset";
        public const string RegularFontFile = FontFolder + "/Pretendard-Regular.otf";
        public const string BoldFontFile = FontFolder + "/Pretendard-Bold.otf";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Tools/Procurement Hero/Setup Game", priority = 1)]
        static void SetupMenu()
        {
            var log = new List<string>();
            bool ok = Run(log, interactive: true);
            string text = string.Join("\n", log);
            Debug.Log("[ProcurementHero] Setup Game\n" + text);
            EditorUtility.DisplayDialog("Procurement Hero", (ok ? "설정을 마쳤습니다.\n\n" : "설정을 끝내지 못했습니다.\n\n") + text, "확인");
        }

        /// <summary>메뉴와 자동화에서 함께 쓰는 본체. interactive가 false면 대화상자를 띄우지 않는다.</summary>
        public static bool Run(List<string> log, bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                log.Add("× 플레이 모드에서는 실행할 수 없습니다.");
                return false;
            }

            // 저장되지 않은 씬 변경을 조용히 버리지 않는다.
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty).ToList();
            if (dirty.Count > 0)
            {
                if (!interactive)
                {
                    log.Add("× 저장되지 않은 씬이 있습니다: " + string.Join(", ", dirty.Select(s => s.name)) + ". 저장 후 다시 실행하세요.");
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.Add("× 저장되지 않은 씬 변경이 있어 취소했습니다.");
                    return false;
                }
            }

            EnsureFolder(DataFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(FontFolder);

            if (!EnsureTmpEssentials(log, interactive)) return false;
            var font = EnsureFontAssets(log);
            if (!font) return false;
            var database = EnsureData(log);
            EnsureScene(database, font, log);
            EnsureBuildSettings(log);
            EnsurePlayerSettings(log);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static bool continueInteractive;

        static bool EnsureTmpEssentials(List<string> log, bool interactive)
        {
            if (Resources.Load<TMP_Settings>("TMP Settings"))
            {
                log.Add("○ TMP Essential Resources 있음");
                return true;
            }
            string full = Path.GetFullPath(TmpEssentials);
            if (!File.Exists(full))
            {
                log.Add("× TMP Essential Resources 패키지를 찾지 못했습니다: " + full);
                return false;
            }
            // 패키지 가져오기는 이 호출이 끝난 뒤에 완료되므로, 완료되면 설정을 자동으로 이어 간다.
            continueInteractive = interactive;
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(full, false);
            log.Add("○ TMP Essential Resources를 가져오는 중입니다. 완료되면 Setup Game이 자동으로 이어서 실행됩니다.");
            return false;
        }

        static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += () =>
            {
                if (continueInteractive)
                {
                    SetupMenu();
                    return;
                }
                var log = new List<string>();
                bool ok = Run(log, false);
                Debug.Log("[ProcurementHero] Setup Game (계속) " + (ok ? "완료" : "실패") + "\n" + string.Join("\n", log));
            };
        }

        static TMP_FontAsset EnsureFontAssets(List<string> log)
        {
            var regular = EnsureFontAsset(RegularFontFile, FontAssetPath, "Pretendard SDF", log);
            var bold = EnsureFontAsset(BoldFontFile, BoldFontAssetPath, "Pretendard Bold SDF", log);
            if (!regular) return null;

            if (bold)
            {
                var table = regular.fontWeightTable;
                if (table != null && table.Length > 7 && table[7].regularTypeface != bold)
                {
                    table[7].regularTypeface = bold;
                    EditorUtility.SetDirty(regular);
                }
            }
            return regular;
        }

        static TMP_FontAsset EnsureFontAsset(string fontPath, string assetPath, string assetName, List<string> log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing)
            {
                log.Add($"○ {assetName} 있음");
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (!source)
            {
                log.Add($"× 한글 폰트 파일이 없습니다: {fontPath} (Docs/SOURCES_AND_LICENSES.md 참고)");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);
            foreach (var tex in asset.atlasTextures)
            {
                tex.name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            // 빌드에는 편집 중 쌓인 글리프를 비우고, 실행 시 원본 폰트에서 다시 만든다.
            var so = new SerializedObject(asset);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null)
            {
                clear.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            log.Add($"○ {assetName} 생성 (동적 SDF, 원본 {Path.GetFileName(fontPath)})");
            return asset;
        }

        static GameDatabase EnsureData(List<string> log)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (!database)
            {
                database = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
                log.Add("○ GameDatabase 생성");
            }

            int created = 0;
            var ordered = new List<MissionData>();
            foreach (var seed in DefaultGameData.Missions())
            {
                string path = $"{DataFolder}/{seed.AssetName}.asset";
                var mission = AssetDatabase.LoadAssetAtPath<MissionData>(path);
                if (!mission)
                {
                    mission = ScriptableObject.CreateInstance<MissionData>();
                    mission.id = seed.Id;
                    mission.title = seed.Title;
                    mission.facility = seed.Facility;
                    mission.facilityName = seed.FacilityName;
                    mission.request = seed.Request;
                    mission.budget = seed.Budget;
                    mission.deadlineDays = seed.Deadline;
                    mission.targetPerformance = seed.Target;
                    mission.itemNameA = seed.ItemA;
                    mission.itemNameB = seed.ItemB;
                    mission.productsA = seed.A.ToList();
                    mission.productsB = seed.B.ToList();
                    mission.reactionSuccess = seed.Success;
                    mission.reactionPartial = seed.Partial;
                    mission.reactionFail = seed.Fail;
                    AssetDatabase.CreateAsset(mission, path);
                    created++;
                }
                ordered.Add(mission);
            }

            // 사용자가 순서를 바꿨거나 미션을 더했다면 그대로 둔다. 비었거나 깨진 경우만 채운다.
            if (database.missions.Count == 0 || database.missions.Any(m => !m))
            {
                database.missions = ordered;
                EditorUtility.SetDirty(database);
                log.Add("○ GameDatabase에 미션 3개 연결");
            }
            log.Add(created > 0 ? $"○ 미션 데이터 {created}개 생성" : "○ 미션 데이터 있음 (덮어쓰지 않음)");
            return database;
        }

        static void EnsureScene(GameDatabase database, TMP_FontAsset font, List<string> log)
        {
            bool exists = File.Exists(ScenePath);
            var scene = exists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = FindInScene<Camera>(scene);
            if (!camera)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Cream;
            camera.transform.position = new Vector3(0, 0, -10);
            if (!camera.GetComponent<AudioListener>() && !FindInScene<AudioListener>(scene))
                camera.gameObject.AddComponent<AudioListener>();

            var canvas = FindInScene<Canvas>(scene);
            if (!canvas)
                canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>() ? canvas.GetComponent<CanvasScaler>() : canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>();

            var eventSystem = FindInScene<EventSystem>(scene);
            if (!eventSystem) eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy) Object.DestroyImmediate(legacy);
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (!module) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (!module.actionsAsset) module.AssignDefaultActions();

            var manager = FindInScene<GameManager>(scene);
            if (!manager) manager = new GameObject("GameManager").AddComponent<GameManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.Add(exists ? "○ 씬 갱신: " + ScenePath : "○ 씬 생성: " + ScenePath);
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found) return found;
            }
            return null;
        }

        static void EnsureBuildSettings(List<string> log)
        {
            // 이미 등록돼 있으면 순서를 바꾸지 않는다(현장 수첩 씬이 시작 씬일 수 있다).
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
            {
                log.Add("○ 카드 비교 씬이 이미 빌드 목록에 있음 (순서 유지)");
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("○ 카드 비교 씬을 빌드 목록에 등록");
        }

        static void EnsurePlayerSettings(List<string> log)
        {
            PlayerSettings.productName = "조달 히어로";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            log.Add("○ 플레이어 설정: 제품명 '조달 히어로', 1920×1080 창 모드");
        }
    }
}
