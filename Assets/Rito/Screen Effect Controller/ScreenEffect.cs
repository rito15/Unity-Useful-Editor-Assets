using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 날짜 : 2021-08-18 PM 10:48:56
// 작성자 : Rito

/*
 * [TODO]
 * 
 * - 수명 설정 시 하이라키에 타이머 아이콘과 함께 남은 수명 실시간 표시
 * - Update로 남은 수명 계산
 * 
 * - 마테리얼 값 변화 이벤트 추가
 * 
 */

namespace Rito
{
    /// <summary> 
    /// 스크린 포스트 이펙트
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class ScreenEffect : MonoBehaviour
    {
        public Material effectMaterial;
        public bool showMaterialNameInHierarchy = true;
        public int priority = 0;
        public float lifespan = 0f;

        private static ScreenEffectController controller;

        private float currentLifespan = 0f;

        private void OnEnable()
        {
            if (controller == null)
                controller = ScreenEffectController.I;

            if (controller != null)
                controller.AddEffect(this);
        }
        private void OnDisable()
        {
            if (controller == null)
                controller = ScreenEffectController.I;

            if (controller != null)
                controller.RemoveEffect(this);
        }

        private void Update()
        {
            if (Application.isPlaying == false) return;
            if (lifespan <= 0f) return;

            currentLifespan += Time.deltaTime;
            if (currentLifespan >= lifespan)
                Destroy(gameObject);
        }

        /***********************************************************************
        *                               Custom Editor
        ***********************************************************************/
        #region .
        // 커스텀 에디터가 아니라 컴포넌트 자체에 마테리얼 프로퍼티 정보, 이벤트 정보 보관

        // 쉐이더 프로퍼티 타입별 값은 각각 개수에 맞게 타이트하게

#if UNITY_EDITOR

        [CustomEditor(typeof(ScreenEffect))]
        private class CE : UnityEditor.Editor
        {
            private readonly struct MaterialPropertyInfo
            {
                public readonly string name;
                public readonly string displayName;
                public readonly ShaderPropertyType type;
                public readonly int propIndex;
                public readonly int valueIndex;

                public MaterialPropertyInfo(string name, string displayName, ShaderPropertyType type, int propIndex, int valueIndex)
                {
                    this.name = name;
                    this.displayName = displayName;
                    this.type = type;
                    this.propIndex = propIndex;
                    this.valueIndex = valueIndex;
                }
            }
            private struct RangeValue
            {
                public float value;
                public float min;
                public float max;
            }
            private struct ColorValue
            {
                public Vector4 vector;
                public Color color;

                public void CopyVectorToColor()
                {
                    color = vector.RefVector4ToColor();
                }
                public void CopyColorToVector()
                {
                    vector = color.ColorToVector4();
                }
            }


            private ScreenEffect m;
            private Material material;
            private Shader shader;

            private List<MaterialPropertyInfo> matPropertyList = new List<MaterialPropertyInfo>(20);
            private int matPropertyCount;

            private string[] matPropertyNameArray;
            private int selectedDropdownIndex = 0;

            private float[] floatValues;
            private RangeValue[] rangeValues;
            private Vector4[] vectorValues;
            private ColorValue[] colorValues;

            private void OnEnable()
            {
                m = target as ScreenEffect;
            }

            public override void OnInspectorGUI()
            {
                Undo.RecordObject(m, "Screen Effect Component");

                EditorGUI.BeginChangeCheck();
                {
                    DrawDefaultVariables();
                    InitVariables();

                    if (material != null)
                    {
                        InitMaterialProperties();
                        DrawMaterialPropertyListDropdown();
                    }
                }
                if (EditorGUI.EndChangeCheck())
                    EditorApplication.RepaintHierarchyWindow();
            }

            private void DrawDefaultVariables()
            {
                m.effectMaterial = EditorGUILayout.ObjectField("Effect Material", m.effectMaterial, typeof(Material), false) as Material;

                m.showMaterialNameInHierarchy = EditorGUILayout.Toggle("Show Material Name", m.showMaterialNameInHierarchy);

                m.priority = EditorGUILayout.IntSlider("Priority", m.priority, -10, 10);

                m.lifespan = EditorGUILayout.FloatField("Lifespan", m.lifespan);
                if (m.lifespan < 0f) m.lifespan = 0f;
            }
            private void InitVariables()
            {
                material = m.effectMaterial;
                shader = material != null ? material.shader : null;
            }
            private void InitMaterialProperties()
            {
                int propertyCount = shader.GetPropertyCount();
                matPropertyList.Clear();

                // Note : 메모리를 희생해서 간결하게 코딩
                if (floatValues == null || floatValues.Length < propertyCount)
                {
                    floatValues  = new float[propertyCount];
                    rangeValues  = new RangeValue[propertyCount];
                    vectorValues = new Vector4[propertyCount];
                    colorValues  = new ColorValue[propertyCount];
                }

                // 쉐이더, 마테리얼 프로퍼티 목록 순회하면서 데이터 가져오기
                int index = 0;
                for (int i = 0; i < propertyCount; i++)
                {
                    ShaderPropertyType propType = shader.GetPropertyType(i);
                    if (propType != ShaderPropertyType.Texture)
                    {
                        string propName = shader.GetPropertyName(i);
                        int propIndex = shader.FindPropertyIndex(propName);
                        string dispName = shader.GetPropertyDescription(propIndex);

                        switch (propType)
                        {
                            case ShaderPropertyType.Float:
                                floatValues[index] = material.GetFloat(propName);
                                break;

                            case ShaderPropertyType.Range:
                                rangeValues[index].value = material.GetFloat(propName);
                                Vector2 minMax = shader.GetPropertyRangeLimits(propIndex);
                                rangeValues[index].min = minMax.x;
                                rangeValues[index].max = minMax.y;
                                break;

                            case ShaderPropertyType.Vector:
                                vectorValues[index] = material.GetVector(propName);
                                break;

                            case ShaderPropertyType.Color:
                                colorValues[index].color = material.GetColor(propName);
                                colorValues[index].CopyColorToVector();
                                break;
                        }

                        matPropertyList.Add(new MaterialPropertyInfo(propName, dispName, propType, propIndex, index++));
                    }
                }

                matPropertyCount = matPropertyList.Count;

                // 프로퍼티 이름 배열 생성
                matPropertyNameArray = matPropertyList // TODO : Where로 현재 이벤트 생성된 프로퍼티는 제외
                    .Select(pInfo => pInfo.displayName)
                    .ToArray();
            }
            private void DrawMaterialPropertyListDropdown()
            {
                selectedDropdownIndex = EditorGUILayout.Popup("Material Properties", selectedDropdownIndex, matPropertyNameArray);
            }
        }
#endif
        #endregion

        /***********************************************************************
        *                               Hierarchy Icon
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        public static string CurrentFolderPath { get; private set; }

        private static Texture2D iconTexture;
        private static string iconTextureFilePath = @"Include\Effect.png";

        [InitializeOnLoadMethod]
        private static void ApplyHierarchyIcon()
        {
            InitFolderPath();

            if (iconTexture == null)
            {
                // "Assets\...\Icon.png"
                string texturePath = System.IO.Path.Combine(CurrentFolderPath, iconTextureFilePath);
                iconTexture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
            }

            EditorApplication.hierarchyWindowItemOnGUI += HierarchyIconHandler;
        }

        private static void InitFolderPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        {
            CurrentFolderPath = System.IO.Path.GetDirectoryName(sourceFilePath);
            int rootIndex = CurrentFolderPath.IndexOf(@"Assets\");
            if (rootIndex > -1)
            {
                CurrentFolderPath = CurrentFolderPath.Substring(rootIndex, CurrentFolderPath.Length - rootIndex);
            }
        }

        static void HierarchyIconHandler(int instanceID, Rect selectionRect)
        {
            GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (go != null)
            {
                var target = go.GetComponent<ScreenEffect>();
                if (target != null)
                {
                    DrawHierarchyGUI(selectionRect, target);
                }
            }
        }

        private static GUIStyle matNameLabelStyle;
        private static GUIStyle priorityLabelStyle;
        private static void DrawHierarchyGUI(in Rect fullRect, ScreenEffect effect)
        {
            GameObject go = effect.gameObject;
            bool goActive = go.activeInHierarchy;
            bool matIsNotNull = effect.effectMaterial != null;

            // 1. Left Icon
            Rect iconRect = new Rect(fullRect);
            iconRect.width = 16f;

#if UNITY_2019_3_OR_NEWER
            iconRect.x = 32f;
#else
            iconRect.x = 0f;
#endif
            if (goActive && matIsNotNull)
            {
                GUI.DrawTexture(iconRect, iconTexture);
            }


            // 2. Right Rects
            float xEnd = fullRect.xMax + 10f;

            Rect rightButtonRect = new Rect(fullRect);
            rightButtonRect.xMax = xEnd;
            rightButtonRect.xMin = xEnd - 36f;

            Rect leftButtonRect = new Rect(rightButtonRect);
            leftButtonRect.xMax = rightButtonRect.xMin - 4f;
            leftButtonRect.xMin = leftButtonRect.xMax - 32f;

            float labelPosX = 20f;
            if (effect.priority <= -10 || effect.priority >= 100)
                labelPosX += 4f;
            if (effect.priority <= -100)
                labelPosX += 8f;

            Rect priorityLabelRect = new Rect(leftButtonRect);
            priorityLabelRect.xMax = leftButtonRect.xMin - 4f;
            priorityLabelRect.xMin = priorityLabelRect.xMax - labelPosX;

            Rect matNameRect = new Rect(priorityLabelRect);
            matNameRect.xMax = priorityLabelRect.xMin - 4f;
            matNameRect.xMin = matNameRect.xMax - 160f;


            // Labels
            if (priorityLabelStyle == null)
                priorityLabelStyle = new GUIStyle(EditorStyles.label);
            if (matNameLabelStyle == null)
                matNameLabelStyle = new GUIStyle(EditorStyles.label);

            priorityLabelStyle.normal.textColor = goActive ? Color.cyan : Color.gray;
            matNameLabelStyle.normal.textColor  = goActive ? Color.magenta * 1.5f : Color.gray;

            EditorGUI.BeginDisabledGroup(!goActive);
            {
                // Priority Label
                GUI.Label(priorityLabelRect, effect.priority.ToString(), priorityLabelStyle);

                // Material Name Label
                if (effect.showMaterialNameInHierarchy && matIsNotNull)
                    GUI.Label(matNameRect, effect.effectMaterial.name, matNameLabelStyle);
            }
            EditorGUI.EndDisabledGroup();


            // Buttons
            EditorGUI.BeginDisabledGroup(go.activeSelf);
            if (GUI.Button(leftButtonRect, "ON"))
            {
                go.SetActive(true);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!go.activeSelf);
            if (GUI.Button(rightButtonRect, "OFF"))
            {
                go.SetActive(false);
            }
            EditorGUI.EndDisabledGroup();
        }
#endif
            #endregion
        /***********************************************************************
        *                               Context Menu
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
    private const string HierarchyMenuItemTitle = "GameObject/Effects/Screen Effect";

    [MenuItem(HierarchyMenuItemTitle, false, 501)]
    private static void MenuItem()
    {
        if (Selection.activeGameObject == null)
        {
            GameObject go = new GameObject("Screen Effect");
            go.AddComponent<ScreenEffect>();
        }
    }

    [MenuItem(HierarchyMenuItemTitle, true)] // Validation
    private static bool MenuItem_Validate()
    {
        return Selection.activeGameObject == null;
    }
#endif
    #endregion
        /***********************************************************************
        *                               Save Playmode Changes
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        private class Inner_PlayModeSave
        {
            private static UnityEditor.SerializedObject[] targetSoArr;

            [UnityEditor.InitializeOnLoadMethod]
            private static void Run()
            {
                UnityEditor.EditorApplication.playModeStateChanged += state =>
                {
                    switch (state)
                    {
                        case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                            var targets = FindObjectsOfType(typeof(Inner_PlayModeSave).DeclaringType);
                            targetSoArr = new UnityEditor.SerializedObject[targets.Length];
                            for (int i = 0; i < targets.Length; i++)
                                targetSoArr[i] = new UnityEditor.SerializedObject(targets[i]);
                            break;

                        case UnityEditor.PlayModeStateChange.EnteredEditMode:
                            foreach (var oldSO in targetSoArr)
                            {
                                if (oldSO.targetObject == null) continue;
                                var oldIter = oldSO.GetIterator();
                                var newSO = new UnityEditor.SerializedObject(oldSO.targetObject);
                                while (oldIter.NextVisible(true))
                                    newSO.CopyFromSerializedProperty(oldIter);
                                newSO.ApplyModifiedProperties();
                            }
                            break;
                    }
                };
            }
        }
#endif
        #endregion
    }
    /***********************************************************************
    *                               Editor Only Extensions
    ***********************************************************************/
    #region .
#if UNITY_EDITOR
    internal static class EditorOnlyExtensions
    {
        public static void RefNotAllowMinus(ref this float @this)
        {
            if (@this < 0f) @this = 0f;
        }
        public static void RefNotAllowMinus(ref this Vector4 @this)
        {
            @this.x.RefNotAllowMinus();
            @this.y.RefNotAllowMinus();
            @this.z.RefNotAllowMinus();
            @this.w.RefNotAllowMinus();
        }
        public static Color RefVector4ToColor(ref this Vector4 @this)
        {
            @this.RefNotAllowMinus();
            return new Color(@this.x, @this.y, @this.z, @this.w);
        }
        public static Vector4 ColorToVector4(in this Color @this)
        {
            return new Vector4(@this.r, @this.g, @this.b, @this.a);
        }
    }
#endif
    #endregion
}