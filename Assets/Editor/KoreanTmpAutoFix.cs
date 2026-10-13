using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class KoreanTmpAutoFix
{
    private const string SourceFontPath = "Assets/Fonts/Bithumb Gothic Basic.ttf";
    private const string TmpFontPath = "Assets/Fonts/BithumbGothic_TMP.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    static KoreanTmpAutoFix()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Tools/Fix Korean TMP Font")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("[Korean TMP] Source font not found: " + SourceFontPath);
            return;
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("[Korean TMP] Failed to create TMP font asset.");
                return;
            }

            fontAsset.name = "BithumbGothic_TMP";

            AssetDatabase.CreateAsset(fontAsset, TmpFontPath);

            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            foreach (Texture2D texture in fontAsset.atlasTextures)
            {
                if (texture != null)
                {
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }
        }

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        string requiredCharacters =
            "Host Client 시작 연결 완료 실행 참여자 명 실패 시도 대기 메시지를 받았습니다 연결 끊김";
        if (!fontAsset.TryAddCharacters(requiredCharacters, out string missing) &&
            !string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning("[Korean TMP] Missing glyphs: " + missing);
        }

        EditorUtility.SetDirty(fontAsset);

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings != null)
        {
            SerializedObject serializedSettings = new SerializedObject(settings);

            SerializedProperty defaultFont = serializedSettings.FindProperty("m_defaultFontAsset");
            if (defaultFont != null)
            {
                defaultFont.objectReferenceValue = fontAsset;
            }

            SerializedProperty fallbackFonts = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (fallbackFonts != null)
            {
                bool exists = false;
                for (int i = 0; i < fallbackFonts.arraySize; i++)
                {
                    if (fallbackFonts.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    int index = fallbackFonts.arraySize;
                    fallbackFonts.InsertArrayElementAtIndex(index);
                    fallbackFonts.GetArrayElementAtIndex(index).objectReferenceValue = fontAsset;
                }
            }

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        bool sceneChanged = false;
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (TMP_Text text in texts)
        {
            if (text == null || EditorUtility.IsPersistent(text))
            {
                continue;
            }

            GameObject go = text.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
            {
                continue;
            }

            if (text.font != fontAsset)
            {
                Undo.RecordObject(text, "Apply Korean TMP Font");
                text.font = fontAsset;
                text.SetAllDirty();
                EditorUtility.SetDirty(text);
                sceneChanged = true;
            }
        }

        if (sceneChanged)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Korean TMP] Fixed. BithumbGothic_TMP is now the default/fallback TMP font and loaded scene TMP texts were updated.");
    }
}
