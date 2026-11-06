using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcurementHero.Field;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProcurementHero.EditorTools
{
    /// <summary>
    /// Tools/Procurement Hero/Setup Field Notebook.
    /// 현장 수첩 버전의 데이터 에셋과 새 씬을 만든다. 기존 카드 비교 씬과 데이터는 건드리지 않는다.
    /// 다시 실행해도 이미 있는 미션 에셋은 덮어쓰지 않고, 씬의 오브젝트는 중복 생성하지 않는다.
    /// </summary>
    public static class FieldSetup
    {
        public const string DataFolder = GameSetup.Root + "/Data/Field";
        public const string DatabasePath = DataFolder + "/FieldDatabase.asset";
        public const string ScenePath = GameSetup.SceneFolder + "/FieldNotebook.unity";

        [MenuItem("Tools/Procurement Hero/Setup Field Notebook", priority = 20)]
        static void SetupMenu()
        {
            var log = new List<string>();
            bool ok = Run(log, interactive: true);
            Debug.Log("[ProcurementHero] Setup Field Notebook\n" + string.Join("\n", log));
            EditorUtility.DisplayDialog("Procurement Hero", (ok ? "현장 수첩 씬을 준비했습니다.\n\n" : "설정을 끝내지 못했습니다.\n\n") + string.Join("\n", log), "확인");
        }

        [MenuItem("Tools/Procurement Hero/Use Field Notebook As Start Scene", priority = 21)]
        static void StartSceneMenu()
        {
            var log = new List<string>();
            SetAsStartScene(log);
            EditorUtility.DisplayDialog("Procurement Hero", string.Join("\n", log), "확인");
        }

        [MenuItem("Tools/Procurement Hero/Reset Field Mission Data To Defaults", priority = 23)]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Procurement Hero", "현장 미션 3개를 기본 데이터(DefaultFieldData)로 덮어씁니다.\nInspector에서 고친 값이 사라집니다. 계속할까요?", "덮어쓰기", "취소"))
                return;
            var log = new List<string>();
            ResetMissionData(log);
            EditorUtility.DisplayDialog("Procurement Hero", string.Join("\n", log), "확인");
        }

        /// <summary>미션 에셋을 기본 데이터로 덮어쓴다(GUID와 파일 이름은 그대로). 명시적으로 실행할 때만 쓴다.</summary>
        public static void ResetMissionData(List<string> log)
        {
            foreach (var seed in DefaultFieldData.Missions())
            {
                string path = $"{DataFolder}/{seed.AssetName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<FieldMissionData>(path);
                if (!existing)
                {
                    log.Add("× 에셋이 없습니다(Setup Field Notebook 먼저): " + path);
                    Object.DestroyImmediate(seed.Data);
                    continue;
                }
                EditorUtility.CopySerialized(seed.Data, existing);
                existing.name = seed.AssetName;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(seed.Data);
                log.Add("○ 기본 데이터로 갱신: " + path);
            }
            AssetDatabase.SaveAssets();
        }

        public static bool Run(List<string> log, bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                log.Add("× 플레이 모드에서는 실행할 수 없습니다.");
                return false;
            }
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty).ToList();
            if (dirty.Count > 0)
            {
                if (!interactive)
                {
                    log.Add("× 저장되지 않은 씬이 있습니다: " + string.Join(", ", dirty.Select(s => s.name)));
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.Add("× 저장되지 않은 씬 변경이 있어 취소했습니다.");
                    return false;
                }
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameSetup.FontAssetPath);
            if (!font)
            {
                log.Add("× 한글 폰트 에셋이 없습니다. 먼저 Tools/Procurement Hero/Setup Game을 실행하세요.");
                return false;
            }

            EnsureFolder(DataFolder);
            var database = EnsureData(log);
            EnsureScene(database, font, log);
            EnsureRegistered(log);
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

        static FieldDatabase EnsureData(List<string> log)
        {
            var database = AssetDatabase.LoadAssetAtPath<FieldDatabase>(DatabasePath);
            if (!database)
            {
                database = ScriptableObject.CreateInstance<FieldDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
                log.Add("○ FieldDatabase 생성");
            }
            int created = 0;
            var ordered = new List<FieldMissionData>();
            foreach (var seed in DefaultFieldData.Missions())
            {
                string path = $"{DataFolder}/{seed.AssetName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<FieldMissionData>(path);
                if (existing)
                {
                    Object.DestroyImmediate(seed.Data);
                    ordered.Add(existing);
                    continue;
                }
                seed.Data.name = seed.AssetName;
                AssetDatabase.CreateAsset(seed.Data, path);
                ordered.Add(seed.Data);
                created++;
            }
            if (database.missions.Count == 0 || database.missions.Any(m => !m))
            {
                database.missions = ordered;
                EditorUtility.SetDirty(database);
            }
            log.Add(created > 0 ? $"○ 현장 미션 데이터 {created}개 생성" : "○ 현장 미션 데이터 있음 (덮어쓰지 않음)");
            return database;
        }

        static void EnsureScene(FieldDatabase database, TMP_FontAsset font, List<string> log)
        {
            bool exists = File.Exists(ScenePath);
            var scene = exists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = Find<Camera>(scene);
            if (!camera)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Navy;
            camera.transform.position = new Vector3(0, 0.6f, -10);

            var canvas = Find<Canvas>(scene);
            if (!canvas)
                canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = Find<EventSystem>(scene);
            if (!eventSystem) eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (!module) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (!module.actionsAsset) module.AssignDefaultActions();

            var game = Find<FieldGame>(scene);
            if (!game) game = new GameObject("FieldGame").AddComponent<FieldGame>();
            var so = new SerializedObject(game);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.Add((exists ? "○ 씬 갱신: " : "○ 씬 생성: ") + ScenePath);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found) return found;
            }
            return null;
        }

        /// <summary>빌드 목록에 새 씬을 올린다. 시작 씬(0번)은 실제 실행을 확인한 뒤 별도 메뉴로 바꾼다.</summary>
        static void EnsureRegistered(List<string> log)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
            {
                log.Add("○ 빌드 목록에 이미 등록됨");
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("○ 빌드 목록에 추가 (시작 씬은 그대로)");
        }

        public static void SetAsStartScene(List<string> log)
        {
            if (!File.Exists(ScenePath))
            {
                log.Add("× 현장 수첩 씬이 없습니다. Setup Field Notebook을 먼저 실행하세요.");
                return;
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("○ 시작 씬(Build Settings 0번): " + ScenePath + " · 기존 씬 항목은 그대로 남김");
        }
    }
}
