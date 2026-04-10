using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Toontown.Editor
{
    public sealed class ToontownEnvironmentSwitcherWindow : EditorWindow
    {
        [Serializable]
        private sealed class EnvironmentPreset
        {
            public string label = "New Preset";
            public Material skybox;
            public AudioClip ambientAudio;
            public float lightIntensity = 1f;
            public Color lightColor = Color.white;
            public bool enableFog;
            public FogMode fogMode = FogMode.ExponentialSquared;
            public Color fogColor = Color.gray;
            public float fogDensity = 0.01f;
            public float fogStartDistance;
            public float fogEndDistance = 300f;
            public GameObject[] enabledEffects = Array.Empty<GameObject>();
        }

        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private AudioSource ambientAudioSource;
        [SerializeField] private GameObject[] managedEffects = Array.Empty<GameObject>();
        [SerializeField] private EnvironmentPreset[] presets = Array.Empty<EnvironmentPreset>();
        [SerializeField] private string statusMessage = "Assign scene references and apply a preset.";

        private Vector2 presetScrollPosition;

        [MenuItem("Toontown/Environment Switcher")]
        public static void ShowWindow()
        {
            GetWindow<ToontownEnvironmentSwitcherWindow>("Toontown Environment");
        }

        private void OnEnable()
        {
            if (presets == null || presets.Length == 0)
            {
                presets = CreateDefaultPresets();
            }

            AutoResolveSceneReferences();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown Environment Preset Switcher", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Switch skybox, lighting, ambient audio, fog, and optional particle effect roots from one editor window.",
                MessageType.Info);

            var serializedWindow = new SerializedObject(this);
            serializedWindow.Update();

            DrawSceneReferences(serializedWindow);
            EditorGUILayout.Space();

            int presetToApply = -1;
            DrawPresetEditor(serializedWindow, ref presetToApply);

            serializedWindow.ApplyModifiedProperties();

            if (presetToApply >= 0 && presetToApply < presets.Length)
            {
                ApplyPreset(presets[presetToApply]);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        private void DrawSceneReferences(SerializedObject serializedWindow)
        {
            EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Find Main Light + Ambient Audio Source"))
            {
                AutoResolveSceneReferences();
            }

            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(mainDirectionalLight)));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(ambientAudioSource)));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(managedEffects)), includeChildren: true);

            if (mainDirectionalLight == null)
            {
                EditorGUILayout.HelpBox(
                    "No directional light assigned. Light color/intensity updates will be skipped until one is assigned.",
                    MessageType.Warning);
            }
        }

        private void DrawPresetEditor(SerializedObject serializedWindow, ref int presetToApply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preset Definitions", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset Defaults", GUILayout.Width(120f)))
            {
                presets = CreateDefaultPresets();
                statusMessage = "Restored default Toontown preset definitions.";
            }
            EditorGUILayout.EndHorizontal();

            SerializedProperty presetsProperty = serializedWindow.FindProperty(nameof(presets));
            if (presetsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No presets defined.", MessageType.Warning);
                return;
            }

            presetScrollPosition = EditorGUILayout.BeginScrollView(presetScrollPosition, GUILayout.MinHeight(280f));
            for (int i = 0; i < presetsProperty.arraySize; i++)
            {
                SerializedProperty presetProperty = presetsProperty.GetArrayElementAtIndex(i);
                DrawPresetCard(presetProperty, i, ref presetToApply);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawPresetCard(SerializedProperty presetProperty, int index, ref int presetToApply)
        {
            SerializedProperty labelProperty = presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.label));
            string buttonLabel = string.IsNullOrWhiteSpace(labelProperty.stringValue)
                ? $"Preset {index + 1}"
                : labelProperty.stringValue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(labelProperty, new GUIContent("Label"));
            if (GUILayout.Button($"Apply {buttonLabel}", GUILayout.Width(120f)))
            {
                presetToApply = index;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.skybox)));
            EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.ambientAudio)));
            EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.lightIntensity)));
            EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.lightColor)));
            EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.enableFog)));

            bool fogEnabled =
                presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.enableFog)).boolValue;
            if (fogEnabled)
            {
                EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogMode)));
                EditorGUILayout.PropertyField(presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogColor)));

                FogMode mode = (FogMode)presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogMode)).enumValueIndex;
                if (mode == FogMode.Linear)
                {
                    EditorGUILayout.PropertyField(
                        presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogStartDistance)));
                    EditorGUILayout.PropertyField(
                        presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogEndDistance)));
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.fogDensity)));
                }
            }

            EditorGUILayout.PropertyField(
                presetProperty.FindPropertyRelative(nameof(EnvironmentPreset.enabledEffects)),
                includeChildren: true);
            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(EnvironmentPreset preset)
        {
            if (preset == null)
            {
                statusMessage = "Cannot apply preset: definition is null.";
                return;
            }

            if (mainDirectionalLight == null)
            {
                AutoResolveSceneReferences();
            }

            bool appliedLight = false;
            if (mainDirectionalLight != null)
            {
                Undo.RecordObject(mainDirectionalLight, "Apply Toontown Environment Preset");
                mainDirectionalLight.intensity = preset.lightIntensity;
                mainDirectionalLight.color = preset.lightColor;
                if (mainDirectionalLight.type == LightType.Directional)
                {
                    RenderSettings.sun = mainDirectionalLight;
                }

                EditorUtility.SetDirty(mainDirectionalLight);
                appliedLight = true;
            }

            RenderSettings.skybox = preset.skybox;
            ApplyFog(preset);
            ApplyAmbientAudio(preset.ambientAudio);
            ApplyManagedEffects(preset.enabledEffects);

            DynamicGI.UpdateEnvironment();
            MarkActiveSceneDirty();
            SceneView.RepaintAll();

            string label = string.IsNullOrWhiteSpace(preset.label) ? "<unnamed>" : preset.label;
            statusMessage = appliedLight
                ? $"Applied preset '{label}'."
                : $"Applied preset '{label}' (light settings skipped: no directional light assigned).";
        }

        private static void ApplyFog(EnvironmentPreset preset)
        {
            RenderSettings.fog = preset.enableFog;
            if (!preset.enableFog)
            {
                return;
            }

            RenderSettings.fogMode = preset.fogMode;
            RenderSettings.fogColor = preset.fogColor;
            if (preset.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = preset.fogStartDistance;
                RenderSettings.fogEndDistance = preset.fogEndDistance;
            }
            else
            {
                RenderSettings.fogDensity = preset.fogDensity;
            }
        }

        private void ApplyAmbientAudio(AudioClip clip)
        {
            if (ambientAudioSource == null)
            {
                return;
            }

            Undo.RecordObject(ambientAudioSource, "Apply Toontown Environment Preset");
            bool clipChanged = ambientAudioSource.clip != clip;
            ambientAudioSource.clip = clip;
            ambientAudioSource.loop = true;

            if (clip == null)
            {
                ambientAudioSource.Stop();
            }
            else if (!ambientAudioSource.isPlaying || clipChanged)
            {
                ambientAudioSource.Play();
            }

            EditorUtility.SetDirty(ambientAudioSource);
        }

        private void ApplyManagedEffects(GameObject[] effectsToEnable)
        {
            var enabledSet = new HashSet<GameObject>();
            if (effectsToEnable != null)
            {
                foreach (GameObject effect in effectsToEnable)
                {
                    if (effect != null)
                    {
                        enabledSet.Add(effect);
                    }
                }
            }

            if (managedEffects != null)
            {
                foreach (GameObject managed in managedEffects)
                {
                    if (managed == null)
                    {
                        continue;
                    }

                    bool shouldBeActive = enabledSet.Contains(managed);
                    if (managed.activeSelf != shouldBeActive)
                    {
                        Undo.RecordObject(managed, "Apply Toontown Environment Preset");
                        managed.SetActive(shouldBeActive);
                        EditorUtility.SetDirty(managed);
                    }
                }
            }

            foreach (GameObject effect in enabledSet)
            {
                if (effect.activeSelf)
                {
                    continue;
                }

                Undo.RecordObject(effect, "Apply Toontown Environment Preset");
                effect.SetActive(true);
                EditorUtility.SetDirty(effect);
            }
        }

        private void AutoResolveSceneReferences()
        {
            if (mainDirectionalLight == null)
            {
                mainDirectionalLight = FindDirectionalLightInActiveScene();
            }

            if (ambientAudioSource == null)
            {
                ambientAudioSource = FindAudioSourceInActiveScene();
            }

            string lightStatus = mainDirectionalLight == null ? "light: missing" : $"light: {mainDirectionalLight.name}";
            string audioStatus = ambientAudioSource == null ? "audio: missing" : $"audio: {ambientAudioSource.name}";
            statusMessage = $"Auto-resolved references ({lightStatus}, {audioStatus}).";
        }

        private static Light FindDirectionalLightInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (Light light in lights)
            {
                if (light == null || light.type != LightType.Directional)
                {
                    continue;
                }

                if (!activeScene.IsValid() || light.gameObject.scene == activeScene)
                {
                    return light;
                }
            }

            return null;
        }

        private static AudioSource FindAudioSourceInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (AudioSource source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                if (!activeScene.IsValid() || source.gameObject.scene == activeScene)
                {
                    return source;
                }
            }

            return null;
        }

        private static void MarkActiveSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static EnvironmentPreset[] CreateDefaultPresets()
        {
            return new[]
            {
                new EnvironmentPreset
                {
                    label = "Classic Toontown",
                    lightIntensity = 1.0f,
                    lightColor = new Color(1f, 0.96f, 0.84f),
                    enableFog = false
                },
                new EnvironmentPreset
                {
                    label = "Toon Shaded",
                    lightIntensity = 1.2f,
                    lightColor = new Color(1f, 1f, 0.9f),
                    enableFog = false
                },
                new EnvironmentPreset
                {
                    label = "Night",
                    lightIntensity = 0.25f,
                    lightColor = new Color(0.7f, 0.78f, 1f),
                    enableFog = true,
                    fogMode = FogMode.ExponentialSquared,
                    fogColor = new Color(0.12f, 0.16f, 0.27f),
                    fogDensity = 0.02f
                },
                new EnvironmentPreset
                {
                    label = "Sunset",
                    lightIntensity = 0.9f,
                    lightColor = new Color(1f, 0.7f, 0.47f),
                    enableFog = true,
                    fogMode = FogMode.ExponentialSquared,
                    fogColor = new Color(1f, 0.6f, 0.4f),
                    fogDensity = 0.015f
                },
                new EnvironmentPreset
                {
                    label = "Stormy",
                    lightIntensity = 0.6f,
                    lightColor = new Color(0.78f, 0.86f, 1f),
                    enableFog = true,
                    fogMode = FogMode.ExponentialSquared,
                    fogColor = new Color(0.31f, 0.35f, 0.43f),
                    fogDensity = 0.03f
                },
                new EnvironmentPreset
                {
                    label = "Gag Themed",
                    lightIntensity = 1.3f,
                    lightColor = new Color(1f, 1f, 0.78f),
                    enableFog = false
                }
            };
        }
    }
}
