using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;
using System.Runtime.InteropServices;

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
        public enum StopAction
        {
            Destroy, Disable
        }

        public Material effectMaterial;
        public bool showMaterialNameInHierarchy = true;
        public int priority = 0;
        public float lifespan = 0f;
        public StopAction stopAction;

        private static ScreenEffectController controller;

        private float currentLifespan = 0f;

        private void OnEnable()
        {
            if (controller == null)
                controller = ScreenEffectController.I;

            if (controller != null)
                controller.AddEffect(this);

            currentLifespan = 0f;
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
            {
                if (stopAction == StopAction.Destroy)
                    Destroy(gameObject);
                else
                    gameObject.SetActive(false);
            }
        }

        /***********************************************************************
        *                           Material Property Events
        ***********************************************************************/
        #region .
        [System.Serializable]
        private class MaterialPropertyInfo
        {
            public Material material;
            public string propName;
            public string displayName;
            public ShaderPropertyType propType;
            public int propIndex;

#if UNITY_EDITOR
            public bool edt_foldout = false;
#endif

            public List<MaterialPropertyEvent> eventList = new List<MaterialPropertyEvent>(10);

            public bool HasEvents => eventList != null && eventList.Count > 0;

            public MaterialPropertyInfo(Material material, string name, string displayName, ShaderPropertyType type, int propIndex)
            {
                this.material = material;
                this.propName = name;
                this.displayName = displayName;
                this.propType = type;
                this.propIndex = propIndex;
            }

            /// <summary> 이벤트가 아예 없었던 경우, 초기 이벤트 2개(시작, 끝) 추가 </summary>
            public void AddInitialEvents(float lifespan)
            {
                switch (propType)
                {
                    case ShaderPropertyType.Float:
                        {
                            float value = material.GetFloat(propName);
                            eventList.Add(new MaterialPropertyEvent() { time = 0f, floatValue = value });
                            eventList.Add(new MaterialPropertyEvent() { time = lifespan, floatValue = value });
                        }
                        break;
                    case ShaderPropertyType.Range:
                        {
                            float value = material.GetFloat(propName);
                            Vector2 range = material.shader.GetPropertyRangeLimits(propIndex);
                            eventList.Add(new MaterialPropertyEvent() { time = 0f, floatValue = value, range = range });
                            eventList.Add(new MaterialPropertyEvent() { time = lifespan, floatValue = value, range = range });
                        }
                        break;
                    case ShaderPropertyType.Color:
                        {
                            Color value = material.GetColor(propName);
                            eventList.Add(new MaterialPropertyEvent() { time = 0f, color = value });
                            eventList.Add(new MaterialPropertyEvent() { time = lifespan, color = value });
                        }
                        break;
                    case ShaderPropertyType.Vector:
                        {
                            Vector4 value = material.GetVector(propName);
                            eventList.Add(new MaterialPropertyEvent() { time = 0f, vector4 = value });
                            eventList.Add(new MaterialPropertyEvent() { time = lifespan, vector4 = value });
                        }
                        break;
                }
            }

            /// <summary> 해당 인덱스의 바로 뒤에 새로운 이벤트 추가 </summary>
            public void AddNewEvent(int index)
            {
                MaterialPropertyEvent prevEvent = eventList[index];

                switch (propType)
                {
                    case ShaderPropertyType.Float:
                        {
                            eventList.Add(new MaterialPropertyEvent() 
                                { time = prevEvent.time, floatValue = prevEvent.floatValue }
                            );
                        }
                        break;
                    case ShaderPropertyType.Range:
                        {
                            eventList.Add(new MaterialPropertyEvent() 
                                { time = prevEvent.time, floatValue = prevEvent.floatValue, range = prevEvent.range }
                            );
                        }
                        break;
                    case ShaderPropertyType.Color:
                        {
                            eventList.Add(new MaterialPropertyEvent() { time = prevEvent.time, color = prevEvent.color });
                        }
                        break;
                    case ShaderPropertyType.Vector:
                        {
                            eventList.Add(new MaterialPropertyEvent() { time = prevEvent.time, vector4 = prevEvent.vector4 });
                        }
                        break;
                }
            }
        }
        [System.Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private class MaterialPropertyEvent
        {
            [FieldOffset(0)] public float time;

            [FieldOffset(4)] public float floatValue;

            [FieldOffset(8)] public Vector2 range;
            [FieldOffset(8)] public float min;
            [FieldOffset(12)] public float max;

            [FieldOffset(4)] public Color color;

            [FieldOffset(4)] public Vector4 vector4;
        }

        [SerializeField]
        private List<MaterialPropertyInfo> matPropertyList = new List<MaterialPropertyInfo>(20);

        #endregion
        /***********************************************************************
        *                               Custom Editor
        ***********************************************************************/
        #region .
        // 쉐이더 프로퍼티 타입별 값은 각각 개수에 맞게 타이트하게

#if UNITY_EDITOR

        [CustomEditor(typeof(ScreenEffect))]
        private class CE : UnityEditor.Editor
        {
            private ScreenEffect m;

            private Material material;
            private Shader shader;

            private bool isMaterialChanged;

            private string[] matPropertyNameArray;
            private int selectedDropdownIndex = 0;

            private static readonly Color PlusButtonColor = Color.green * 1.5f;
            private static readonly Color MinusButtonColor = Color.red * 1.5f;
            private static readonly Color MinusButtonColor2 = new Color(1.5f, 0.3f, 0.7f, 1f);

            private static GUIStyle bigMinusButtonStyle;

            private void OnEnable()
            {
                m = target as ScreenEffect;
            }

            public override void OnInspectorGUI()
            {
                isMaterialChanged = CheckMaterialChanged();
                InitVariables();
                InitStyles();

                Undo.RecordObject(m, "Screen Effect Component");

                EditorGUI.BeginChangeCheck();
                {
                    DrawDefaultFields();

                    if (m.effectMaterial == null)
                    {
                        m.matPropertyList.Clear();
                        Debug.Log("NULL Material");
                    }
                    else
                    {
                        // 마테리얼 정보가 변한 경우, 전체 마테리얼 프로퍼티 및 이벤트 목록 초기화
                        if (isMaterialChanged)
                        {
                            InitVariables(); 
                            InitMaterialProperties();
                            //Debug.Log("Material Changed");
                        }

                        EditorGUILayout.Space();
                        DrawMaterialPropertyListDropdown();

                        EditorGUILayout.Space();
                        DrawMaterialPropertyList();
                    }
                }
                if (EditorGUI.EndChangeCheck())
                    EditorApplication.RepaintHierarchyWindow();
            }

            /************************************************************************
             *                          Tiny Methods, Init Methods
             ************************************************************************/
            #region .
            private bool CheckMaterialChanged()
            {


                return false;
            }
            private void InitVariables()
            {
                material = m.effectMaterial;
                shader = material != null ? material.shader : null;
            }
            private void InitStyles()
            {
                if (bigMinusButtonStyle == null)
                {
                    bigMinusButtonStyle = new GUIStyle("button")
                    {
                        fontSize = 20,
                        fontStyle = FontStyle.Bold
                    };
                }
            }
            private void InitMaterialProperties()
            {
                int propertyCount = shader.GetPropertyCount();
                m.matPropertyList.Clear();

                // 쉐이더, 마테리얼 프로퍼티 목록 순회하면서 데이터 가져오기
                for (int i = 0; i < propertyCount; i++)
                {
                    ShaderPropertyType propType = shader.GetPropertyType(i);
                    if (propType != ShaderPropertyType.Texture)
                    {
                        string propName = shader.GetPropertyName(i);
                        int propIndex = shader.FindPropertyIndex(propName);
                        string dispName = shader.GetPropertyDescription(propIndex);

                        m.matPropertyList.Add(new MaterialPropertyInfo(material, propName, dispName, propType, propIndex));
                    }
                }
            }
            #endregion
            /***********************************************************************
            *                             Tiny Drawing Methods
            ***********************************************************************/
            #region .
            private bool DrawButtonLayout(string label, in Color buttonColor, in float width)
            {
                Color bCol = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;

                bool pressed = GUILayout.Button(label, GUILayout.Width(width));

                GUI.backgroundColor = bCol;
                return pressed;
            }
            private bool DrawButton(in Rect rect, string label, in Color buttonColor, GUIStyle style = null)
            {
                Color bCol = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;

                bool pressed = style != null ? GUI.Button(rect, label, style) : GUI.Button(rect, label);

                GUI.backgroundColor = bCol;
                return pressed;
            }
            private void DrawHorizontalSpace(float width)
            {
                EditorGUILayout.LabelField("", GUILayout.Width(width));
            }
            private bool DrawPlusButton(in float width = 40f)
            {
                return DrawButtonLayout("+", PlusButtonColor, width);
            }
            private bool DrawMinusButton(in float width = 40f)
            {
                return DrawButtonLayout("-", MinusButtonColor, width);
            }
            #endregion
            /************************************************************************
             *                               Drawing Methods
             ************************************************************************/
            #region .

            private void DrawDefaultFields()
            {
                EditorGUI.BeginChangeCheck();
                m.effectMaterial = EditorGUILayout.ObjectField("Effect Material", m.effectMaterial, typeof(Material), false) as Material;
                if (EditorGUI.EndChangeCheck())
                    isMaterialChanged = true;

                m.showMaterialNameInHierarchy = EditorGUILayout.Toggle("Show Material Name", m.showMaterialNameInHierarchy);

                m.priority = EditorGUILayout.IntSlider("Priority", m.priority, -10, 10);

                m.lifespan = EditorGUILayout.FloatField("Lifespan", m.lifespan);
                if (m.lifespan < 0f) m.lifespan = 0f;

                m.stopAction = (StopAction)EditorGUILayout.EnumPopup("Stop Action", m.stopAction);
            }

            private void DrawMaterialPropertyListDropdown()
            {
                // 프로퍼티 이름 배열 생성
                matPropertyNameArray = m.matPropertyList
                    .Where(mpi => mpi.eventList == null || mpi.eventList.Count == 0) // 생성된 이벤트가 존재하지 않는 경우만 대상
                    .Select(pInfo => pInfo.displayName)
                    .ToArray();

                // 생성할 수 있는 프로퍼티가 없을 경우, 드롭다운 미표시
                if (matPropertyNameArray.Length == 0)
                    return;

                EditorGUILayout.BeginHorizontal();

                selectedDropdownIndex = EditorGUILayout.Popup("Material Properties", selectedDropdownIndex, matPropertyNameArray);

                // 드롭다운에 지정된 프로퍼티에 대해 이벤트 추가
                if (DrawPlusButton())
                {
                    int found = m.matPropertyList.FindIndex(mp => mp.displayName == matPropertyNameArray[selectedDropdownIndex]);
                    m.matPropertyList[found].AddInitialEvents(m.lifespan);
                }

                EditorGUILayout.EndHorizontal();
            }

            /// <summary> 프로퍼티들 모두 그리기 </summary>
            private void DrawMaterialPropertyList()
            {
                EditorGUILayout.LabelField("Material Property Events", EditorStyles.boldLabel);

                EditorGUILayout.Space();

                for (int i = 0; i < m.matPropertyList.Count; i++)
                {
                    if (m.matPropertyList[i].HasEvents)
                    {
                        DrawPropertyEvents(m.matPropertyList[i], () =>
                            {
                                // [-] 버튼 클릭하면 해당 프로퍼티에서 이벤트들 싹 제거
                                m.matPropertyList[i].eventList.Clear();
                            }
                        );

                        EditorGUILayout.Space();
                    }
                }
            }

            /// <summary> 프로퍼티 하나의 이벤트 모두 그리기 </summary>
            private void DrawPropertyEvents(in MaterialPropertyInfo matProp, Action removeAction)
            {
                EditorGUILayout.BeginHorizontal();

                matProp.edt_foldout = EditorGUILayout.Foldout(matProp.edt_foldout, $"{matProp.displayName} [{matProp.propType}]");;
                if (DrawMinusButton())
                {
                    removeAction();
                    return;
                }

                EditorGUILayout.EndHorizontal();

                if (matProp.edt_foldout)
                {
                    EditorGUI.indentLevel++;

                    int addNewEvent = -1;

                    var eventList = matProp.eventList;
                    for (int i = 0; i < eventList.Count; i++)
                    {
                        // 이벤트 항목 한개 그리기
                        DrawEachEvent(matProp, eventList[i], i);

                        // 이벤틑 사이사이 [+] 버튼 : 새로운 이벤트 추가
                        if (i < eventList.Count - 1)
                        {
                            EditorGUILayout.BeginHorizontal();

                            const float ButtonWidth = 24f;

                            // 버튼 중앙 정렬
                            DrawHorizontalSpace((EditorGUIUtility.currentViewWidth) * 0.5f - ButtonWidth);
                            if (DrawPlusButton(ButtonWidth)) addNewEvent = i;

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    if(addNewEvent > -1)
                        matProp.AddNewEvent(addNewEvent);

                    EditorGUI.indentLevel--;
                }
            }

            /// <summary> 프로퍼티의 이벤트 하나 그리기 </summary>
            private void DrawEachEvent(MaterialPropertyInfo mp, MaterialPropertyEvent mpEvent, int index)
            {
                bool isFirstOrLast = index == 0 || index == mp.eventList.Count - 1;

                if (index == 0) mpEvent.time = 0f;
                else if (index == mp.eventList.Count - 1) mpEvent.time = m.lifespan;
                else
                {
                    MaterialPropertyEvent prevEvent = mp.eventList[index - 1];
                    MaterialPropertyEvent nextEvent = mp.eventList[index + 1];

                    if (prevEvent.time > mpEvent.time)
                        mpEvent.time = prevEvent.time;
                    else if (mpEvent.time > nextEvent.time)
                        mpEvent.time = nextEvent.time;
                }

                // 1. Time 슬라이더
                if (isFirstOrLast) EditorGUI.BeginDisabledGroup(true);

                const float MinusButtonWidth = 40f;

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Time", GUILayout.Width(60f));
                    mpEvent.time = EditorGUILayout.Slider(mpEvent.time, 0f, m.lifespan);

                    // 여백 생성
                    DrawHorizontalSpace(MinusButtonWidth);
                    Rect minusButtonRect = GUILayoutUtility.GetLastRect();
                    minusButtonRect.height *= mp.propType == ShaderPropertyType.Color ? 3f : 2f;

                    // 이 이벤트 제거 버튼
                    if (isFirstOrLast == false)
                    {
                        if (DrawButton(minusButtonRect, "-", MinusButtonColor2, bigMinusButtonStyle))
                            mp.eventList.RemoveAt(index);
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (isFirstOrLast) EditorGUI.EndDisabledGroup();


                // 2. 값 그리기
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Value", GUILayout.Width(60f));
                switch (mp.propType)
                {
                    case ShaderPropertyType.Float:
                        mpEvent.floatValue = EditorGUILayout.FloatField(mpEvent.floatValue);
                        break;

                    case ShaderPropertyType.Range:
                        mpEvent.floatValue = EditorGUILayout.Slider(mpEvent.floatValue, mpEvent.min, mpEvent.max);
                        break;

                    case ShaderPropertyType.Vector:
                        mpEvent.vector4 = EditorGUILayout.Vector4Field("", mpEvent.vector4);
                        break;

                    case ShaderPropertyType.Color:
                        EditorGUILayout.BeginVertical();

                        mpEvent.vector4.RefClamp_000();

                        mpEvent.color = EditorGUILayout.ColorField(mpEvent.color);
                        mpEvent.vector4 = EditorGUILayout.Vector4Field("", mpEvent.vector4);

                        EditorGUILayout.EndVertical();
                        break;
                }

                DrawHorizontalSpace(MinusButtonWidth);

                EditorGUILayout.EndHorizontal();
            }
            #endregion
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
        public static void RefClamp_000(ref this float @this)
        {
            @this *= 1000f;
            @this = (int)@this * 0.001f;
        }
        public static void RefClamp_000(ref this Vector4 @this)
        {
            RefClamp_000(ref @this.x);
            RefClamp_000(ref @this.y);
            RefClamp_000(ref @this.z);
            RefClamp_000(ref @this.w);
        }
    }
#endif
    #endregion
}