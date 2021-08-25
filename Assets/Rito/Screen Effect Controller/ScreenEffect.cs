using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_2019_3_OR_NEWER
using ShaderPropertyType = UnityEngine.Rendering.ShaderPropertyType;
#else
public enum ShaderPropertyType
{
    Color = 0,
    Vector = 1,
    Float = 2,
    Range = 3,
    Texture = 4
}
#endif

// 날짜 : 2021-08-18 PM 10:48:56
// 작성자 : Rito

/*
 * [에디터 테스트]
 *  - 2018.3.14f1 테스트 완료
 *  - 2019.4.9f1  테스트 완료
 *  - 2020.3.14f1 테스트 완료
 *  - 2021.1.16f1 테스트 완료
 * 
 * [빌드 테스트]
 *  - 2018.3.14f1 테스트 완료
 *  - 2019.4.9f1  테스트 완료
 *  - 2020.3.14f1 테스트 완료(Mono, IL2CPP)
 *  - 2021.1.16f1 테스트 완료
 */

/*
 * [Future Works]
 * 
 * - 편집모드 체크된 상태(이벤트 진행 멈춤)에서, 각 그래프를 클릭하면 해당 지점으로 currentTime 또는 currentFrame 이동시키기
 * - 구현 완료하고 각종 테스트 진행
 * 
 * - 구현 전부 완료하고 2018 ~ 2021 빌드 테스트 진행
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
            Destroy, Disable, Repeat
        }

        public Material effectMaterial;
        public int priority = 0;

        [SerializeField] private StopAction stopAction;

        private static ScreenEffectController controller;

        private const int ReferenceFPS = 60;
        private const float ReferenceSPF = 1f / (float)ReferenceFPS;

        /// <summary> 시간 계산 방식이 초인지 프레임인지 여부 </summary>
        [SerializeField] private bool isTimeModeSeconds = true;

        // 지속시간 : 초
        [SerializeField] private float durationSeconds = 0f;
        private float currentSeconds = 0f;

        // 지속시간 : 프레임
        [SerializeField] private int durationFrame = 0;
        private int currentFrame = 0;

#if UNITY_EDITOR
        /// <summary> 플레이 모드 중 Current Time 직접 수정 가능 모드 </summary>
        private bool __editMode = false;

        [SerializeField] private bool showMaterialNameInHierarchy = true;
        [SerializeField] private bool __optionFoldout1 = true;
        [SerializeField] private bool __optionFoldout2 = true;
        [SerializeField] private bool __matPropListFoldout = true;

        [SerializeField] private int __propCount; // 마테리얼 프로퍼티 개수 기억(변화 감지용)

        /// <summary> 마테리얼의 초깃값 기억 </summary>
        [SerializeField]
        private MaterialPropertyValue[] __materialDefaultValues;

        /// <summary> 복제된 마테리얼 현재 값 기억 (Undo 용도)</summary>
        [SerializeField]
        private MaterialPropertyValue[] __materialCurrentValues;

        private Action __OnEditorUpdate;
#endif

        private void OnEnable()
        {
            if (controller == null)
                controller = ScreenEffectController.I;

            if (controller != null)
                controller.AddEffect(this);

            currentSeconds = 0f;
            currentFrame = 0;
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

            UpdateMaterialProperties();

#if UNITY_EDITOR
            if (__editMode) return;
            else __OnEditorUpdate?.Invoke();
#endif
            UpdateTime();
        }

        private void UpdateMaterialProperties()
        {
            if (isTimeModeSeconds)
            {
                if (durationSeconds <= 0f)
                    return;
            }
            else
            {
                if (durationFrame == 0)
                    return;
            }

            for (int i = 0; i < matPropertyList.Count; i++)
            {
                var mp = matPropertyList[i];
                if (mp == null || mp.eventList == null || mp.eventList.Count == 0)
                    continue;

                if (mp.enabled == false)
                    continue;

                var eventList = mp.eventList;
                int eventCount = eventList.Count - 1;

                // 최적화를 위해 추가 : 처리 완료 확인
                bool handled = false;

                // 1. 시간 계산 방식 : 초
                if (isTimeModeSeconds)
                {
#if UNITY_EDITOR
                    // 현재 재생 중인 인덱스 초기화
                    for (int j = 0; j < eventCount; j++)
                    {
                        if (eventList[j].time <= currentSeconds && currentSeconds < eventList[j + 1].time)
                        {
                            mp.__playingIndex = j;
                            break;
                        }
                    }
#endif
                    switch (mp.propType)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                // 해당하는 시간 구간이 아닐 경우, 판정하지 않음
                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);

                                // REMAP
                                float curValue = Mathf.Lerp(prevEvent.floatValue, nextEvent.floatValue, t);

                                effectMaterial.SetFloat(mp.propName, curValue);
                                handled = true;
                            }
                            break;

                        case ShaderPropertyType.Color:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);
                                Color curValue = Color.Lerp(prevEvent.color, nextEvent.color, t);

                                effectMaterial.SetColor(mp.propName, curValue);
                                handled = true;
                            }
                            break;

                        case ShaderPropertyType.Vector:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);
                                Vector4 curValue = Vector4.Lerp(prevEvent.vector4, nextEvent.vector4, t);

                                effectMaterial.SetVector(mp.propName, curValue);
                                handled = true;
                            }
                            break;
                    }
                }
                // 2. 시간 계산 방식 : 프레임
                else
                {
#if UNITY_EDITOR
                    // 현재 재생 중인 인덱스 초기화
                    for (int j = 0; j < eventCount; j++)
                    {
                        if (eventList[j].frame <= currentFrame && currentFrame < eventList[j + 1].frame)
                        {
                            mp.__playingIndex = j;
                            break;
                        }
                    }
#endif
                    switch (mp.propType)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                float curValue = Mathf.Lerp(prevEvent.floatValue, nextEvent.floatValue, t);

                                effectMaterial.SetFloat(mp.propName, curValue);
                                handled = true;
                            }
                            break;

                        case ShaderPropertyType.Color:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                Color curValue = Color.Lerp(prevEvent.color, nextEvent.color, t);

                                effectMaterial.SetColor(mp.propName, curValue);
                                handled = true;
                            }
                            break;

                        case ShaderPropertyType.Vector:
                            for (int j = 0; j < eventCount; j++)
                            {
                                if (handled) break;

                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                Vector4 curValue = Vector4.Lerp(prevEvent.vector4, nextEvent.vector4, t);

                                effectMaterial.SetVector(mp.propName, curValue);
                                handled = true;
                            }
                            break;
                    }
                }
            }
        }

        private void UpdateTime()
        {
            if (isTimeModeSeconds)
                UpdateSeconds();
            else
                UpdateFrames();
        }

        private void UpdateSeconds()
        {
            if (durationSeconds <= 0f) return;

            currentSeconds += Time.deltaTime;
            if (currentSeconds >= durationSeconds)
            {
                switch (stopAction)
                {
                    case StopAction.Destroy:
                        Destroy(gameObject);
                        break;
                    case StopAction.Disable:
                        gameObject.SetActive(false);
                        break;
                        //case StopAction.Repeat:
                        //    currentSeconds = 0f;
                        //    break;
                }

                currentSeconds = 0f;
            }
        }

        private void UpdateFrames()
        {
            if (durationFrame <= 0) return;

            currentFrame++;

            if (currentFrame >= durationFrame)
            {
                switch (stopAction)
                {
                    case StopAction.Destroy:
                        Destroy(gameObject);
                        break;
                    case StopAction.Disable:
                        gameObject.SetActive(false);
                        break;
                }

                currentFrame = 0;
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
            public ShaderPropertyType propType;
            public bool enabled;

            public List<MaterialPropertyValue> eventList;

#if UNITY_EDITOR
            public bool __HasEvents => eventList != null && eventList.Count > 0;

            public string __displayName;
            public int __propIndex;

            public bool __foldout = true;
            public int __playingIndex = 0; // 현재 재생 중인 이벤트의 인덱스

            // 그래프 보여주기
            public bool __showGraphFloat = false;
            public bool[] __showGraphVector;

            public MaterialPropertyInfo(Material material, string name, string displayName, ShaderPropertyType type, int propIndex)
            {
                this.material = material;
                this.propName = name;
                this.__displayName = displayName;
                this.propType = type;
                this.__propIndex = propIndex;
                this.enabled = false;

                this.eventList = new List<MaterialPropertyValue>(10);

                this.__showGraphVector = new bool[4];
                for (int i = 0; i < this.__showGraphVector.Length; i++)
                    this.__showGraphVector[i] = false;
            }

            /// <summary> 이벤트가 아예 없었던 경우, 초기 이벤트 2개(시작, 끝) 추가 </summary>
            public void Edt_AddInitialEvents(float duration, bool isTimeModeSeconds)
            {
                this.enabled = true;

                var begin = new MaterialPropertyValue();
                var end = new MaterialPropertyValue();

                begin.time = 0;

                if (isTimeModeSeconds)
                    end.time = duration;
                else
                    end.frame = (int)duration;

                switch (propType)
                {
                    case ShaderPropertyType.Float:
                        begin.floatValue = end.floatValue = material.GetFloat(propName);
                        break;

                    case ShaderPropertyType.Range:
                        begin.floatValue = end.floatValue = material.GetFloat(propName);
                        begin.range = end.range = material.shader.GetPropertyRangeLimits(__propIndex);
                        break;
                    case ShaderPropertyType.Color:
                        begin.color = end.color = material.GetColor(propName);
                        break;

                    case ShaderPropertyType.Vector:
                        begin.vector4 = end.vector4 = material.GetVector(propName);
                        break;
                }

                eventList.Add(begin);
                eventList.Add(end);
            }

            /// <summary> 해당 인덱스의 바로 뒤에 새로운 이벤트 추가 </summary>
            public void Edt_AddNewEvent(int index, bool isTimeModeSeconds)
            {
                MaterialPropertyValue prevEvent = eventList[index];
                MaterialPropertyValue nextEvent = eventList[index + 1];

                var newValue = new MaterialPropertyValue();

                // 시간은 중간 값으로 전달
                if (isTimeModeSeconds)
                    newValue.time = (prevEvent.time + nextEvent.time) * 0.5f;
                else
                    newValue.frame = (prevEvent.frame + nextEvent.frame) / 2;

                // 현재 마테리얼로부터 값 가져와 초기화
                Edt_SetPropertyValueFromMaterial(newValue);

                eventList.Insert(index + 1, newValue);
            }

            private void Edt_SetPropertyValueFromMaterial(MaterialPropertyValue dest)
            {
                switch (propType)
                {
                    case ShaderPropertyType.Float:
                        dest.floatValue = material.GetFloat(propName);
                        break;

                    case ShaderPropertyType.Range:
                        dest.floatValue = material.GetFloat(propName);
                        dest.range = material.shader.GetPropertyRangeLimits(__propIndex);
                        break;

                    case ShaderPropertyType.Color:
                        dest.color = material.GetColor(propName);
                        break;

                    case ShaderPropertyType.Vector:
                        dest.vector4 = material.GetVector(propName);
                        break;
                }
            }

            /// <summary> 프로퍼티 내의 모든 이벤트 제거 </summary>
            public void Edt_RemoveAllEvents()
            {
                this.eventList.Clear();
                this.enabled = false;
            }
#endif
        }

        [System.Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private class MaterialPropertyValue
        {
            [FieldOffset(0)] public float time;
            [FieldOffset(4)] public int frame;

            [FieldOffset(8)] public float floatValue;

            [FieldOffset(12)] public Vector2 range;
            [FieldOffset(12)] public float min;
            [FieldOffset(16)] public float max;

            [FieldOffset(8)] public Color color;
            [FieldOffset(8)] public Vector4 vector4;
        }

        [SerializeField]
        private List<MaterialPropertyInfo> matPropertyList = new List<MaterialPropertyInfo>(20);

        #endregion
        /***********************************************************************
        *                           Custom Editor
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        [CustomEditor(typeof(ScreenEffect))]
        private class CE : UnityEditor.Editor
        {
            private ScreenEffect m;

            private Material material;
            private Shader shader;

            private bool isMaterialChanged;

            /// <summary> Duration Time 또는 Frame 값이 0 </summary>
            private bool isDurationZero;

            private bool isPlayMode;

            /// <summary> 현재 시간 진행도(0.0 ~ 1.0) </summary>
            private float currentTimeOrFrameRatio;

            private static readonly Color MinusButtonColor = Color.red * 1.5f;
            private static readonly Color TimeColor = new Color(1.5f, 1.5f, 0.2f, 1f);  // Yellow
            private static readonly Color EnabledColor = new Color(0f, 1.5f, 1.5f, 1f); // Cyan

            private static GUIStyle bigMinusButtonStyle;
            private static GUIStyle whiteTextButtonStyle; // 글씨가 하얀색인 버튼
            private static GUIStyle boldFoldoutStyle;
            private static GUIStyle propertyEventTimeLabelStyle;
            private static GUIStyle whiteBoldLabelStyle;
            private static GUIStyle yellowBoldLabelStyle;

            private void OnEnable()
            {
                m = target as ScreenEffect;

                isHangle = EditorPrefs.GetBool(EngHanPrefKey, false);

                m.__OnEditorUpdate -= Repaint;
                m.__OnEditorUpdate += Repaint;

                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                Undo.undoRedoPerformed += OnUndoRedoPerformed;

                InitReflectionData();
            }
            private void OnDisable()
            {
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            }

            /// <summary> 마테리얼 프로퍼티 값 수정 후 Undo/Redo 동작 시 정상적으로 적용 </summary>
            private void OnUndoRedoPerformed()
            {
                if (material == null) return;
                if (m.__materialCurrentValues == null) return;
                if (m.matPropertyList == null) return;

                for (int i = 0; i < m.__materialCurrentValues.Length; i++)
                {
                    if (m.__materialCurrentValues[i] == null) continue;
                    if (m.matPropertyList[i] == null) continue;

                    var curValue = m.__materialCurrentValues[i];
                    var propType = m.matPropertyList[i].propType;
                    var propName = m.matPropertyList[i].propName;

                    switch (propType)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            material.SetFloat(propName, curValue.floatValue);
                            break;

                        case ShaderPropertyType.Vector:
                            material.SetVector(propName, curValue.vector4);
                            break;

                        case ShaderPropertyType.Color:
                            material.SetColor(propName, curValue.color);
                            break;
                    }
                }
            }

            public override void OnInspectorGUI()
            {
                DrawEngHanButton();

                isMaterialChanged = CheckMaterialChanged();
                InitVariables();
                InitStyles();

                Undo.RecordObject(m, "Screen Effect Component");

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.Space();
                    DrawDefaultFields();

                    if (m.effectMaterial == null)
                    {
                        m.matPropertyList.Clear();
                    }
                    else
                    {
                        // 마테리얼 정보가 변한 경우, 전체 마테리얼 프로퍼티 및 이벤트 목록 초기화
                        if (isMaterialChanged)
                        {
                            InitMaterial();
                        }

                        if (isPlayMode)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                            DrawEditorOptions();
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        DrawCopiedMaterialProperties();

                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        DrawMaterialPropertyEventList();
                    }
                }
                if (EditorGUI.EndChangeCheck())
                    EditorApplication.RepaintHierarchyWindow();

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            /***********************************************************************
            *                               Eng / Han
            ***********************************************************************/
            #region .
            private bool isHangle = false;
            private static readonly string EngHanPrefKey = "Rito_ScreenEffect_Hangle";

            private static readonly string[] StopActionsHangle = new string[]
            {
                "파괴", "비활성화", "반복(재시작)"
            };
            private static readonly string[] TimeModesEng = new string[]
            {
                "Time(Seconds)", "Frame"
            };
            private static readonly string[] TimeModesHan = new string[]
            {
                "시간(초)", "프레임"
            };

            private void DrawEngHanButton()
            {
                Rect rect = GUILayoutUtility.GetRect(1f, 20f);
                const float buttonWidth = 44f;
                rect.xMin = rect.width - buttonWidth - 4f;

                if (GUI.Button(rect, "Eng/한글"))
                {
                    isHangle = !isHangle;
                    EditorPrefs.SetBool(EngHanPrefKey, isHangle);
                }
            }

            private string EngHan(string eng, string han)
            {
                return !isHangle ? eng : han;
            }

            #endregion
            /************************************************************************
             *                          Tiny Methods, Init Methods
             ************************************************************************/
            #region .
            private bool CheckMaterialChanged()
            {
                // 쉐이더 프로퍼티 개수 변경 감지
                if (m.effectMaterial != null)
                {
                    int propCount = m.effectMaterial.shader.GetPropertyCount();
                    if (propCount != m.__propCount)
                    {
                        m.__propCount = propCount;
                        return true;
                    }
                }

                return false;
            }
            private void InitReflectionData()
            {
                if (fiCurveBGColor == null)
                {
                    Type type = typeof(EditorGUI);
                    fiCurveBGColor = type.GetField("kCurveBGColor", BindingFlags.Static | BindingFlags.NonPublic);
                    defaultCurveBGColor = (Color)fiCurveBGColor.GetValue(null);
                }
            }
            private void InitVariables()
            {
                isPlayMode = Application.isPlaying;
                LoadMaterialShaderData();

                if (m.isTimeModeSeconds)
                {
                    if (m.durationSeconds <= 0f)
                        currentTimeOrFrameRatio = 0f;
                    else
                    {
                        currentTimeOrFrameRatio = m.currentSeconds / m.durationSeconds;
                    }
                }
                else
                {
                    if (m.durationFrame <= 0)
                        currentTimeOrFrameRatio = 0f;
                    else
                    {
                        currentTimeOrFrameRatio = (float)m.currentFrame / m.durationFrame;
                    }
                }
            }
            private void InitMaterial()
            {
                LoadMaterialShaderData();
                LoadMaterialProperties();
            }
            private void LoadMaterialShaderData()
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
                if (whiteTextButtonStyle == null)
                {
                    whiteTextButtonStyle = new GUIStyle("button")
                    {
                        fontStyle = FontStyle.Bold
                    };
                    whiteTextButtonStyle.normal.textColor = Color.white;
                    whiteTextButtonStyle.hover.textColor = Color.white;
                }
                if (boldFoldoutStyle == null)
                {
                    boldFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                if (propertyEventTimeLabelStyle == null)
                {
                    propertyEventTimeLabelStyle = new GUIStyle(EditorStyles.label);
                    propertyEventTimeLabelStyle.normal.textColor = TimeColor;
                }
                if (whiteBoldLabelStyle == null)
                {
                    whiteBoldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                    whiteBoldLabelStyle.normal.textColor = Color.white;
                }
                if (yellowBoldLabelStyle == null)
                {
                    yellowBoldLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                    yellowBoldLabelStyle.normal.textColor = Color.yellow;
                }
            }
            private void LoadMaterialProperties()
            {
                // 기존 이벤트들 백업
                var backup = m.matPropertyList;

                int propertyCount = shader.GetPropertyCount();
                m.matPropertyList = new List<MaterialPropertyInfo>(propertyCount);

                // 쉐이더, 마테리얼 프로퍼티 목록 순회하면서 데이터 가져오기
                for (int i = 0; i < propertyCount; i++)
                {
                    ShaderPropertyType propType = shader.GetPropertyType(i);
                    if ((int)propType != 4) // 4 : Texture
                    {
                        string propName = shader.GetPropertyName(i);
#if UNITY_2019_3_OR_NEWER
                        int propIndex = shader.FindPropertyIndex(propName);
#else
                        int propIndex = i;
#endif
                        string dispName = shader.GetPropertyDescription(propIndex);

                        m.matPropertyList.Add(new MaterialPropertyInfo(material, propName, dispName, propType, propIndex));
                    }
                }

                int validPropCount = m.matPropertyList.Count;

                // 동일 쉐이더일 경우, 백업된 이벤트들에서 동일하게 존재하는 프로퍼티에 이벤트 복제
                if (validPropCount > 0 && m.matPropertyList[0].material.shader == shader)
                {
                    for (int i = 0; i < validPropCount; i++)
                    {
                        MaterialPropertyInfo cur = m.matPropertyList[i];
                        MaterialPropertyInfo found = backup.Find(x =>
                            x.__HasEvents &&
                            x.propName == cur.propName &&
                            x.propType == cur.propType
                        );

                        if (found != null)
                        {
                            cur.eventList = found.eventList;
                            cur.enabled = found.enabled;
                            cur.__foldout = found.__foldout;
                        }
                    }
                }

                // 마테리얼 기본 값들 기억, 현재 값들 저장
                m.__materialDefaultValues = new MaterialPropertyValue[validPropCount];
                m.__materialCurrentValues = new MaterialPropertyValue[validPropCount];
                for (int i = 0; i < validPropCount; i++)
                {
                    var currentInfo = m.matPropertyList[i];
                    var backupValue = m.__materialDefaultValues[i] = new MaterialPropertyValue();
                    var currentValue = m.__materialCurrentValues[i] = new MaterialPropertyValue();

                    switch (currentInfo.propType)
                    {
                        case ShaderPropertyType.Float:
                            backupValue.floatValue = currentValue.floatValue =
                                material.GetFloat(currentInfo.propName);
                            break;

                        case ShaderPropertyType.Range:
                            backupValue.floatValue = currentValue.floatValue =
                                material.GetFloat(currentInfo.propName);

                            currentValue.range = shader.GetPropertyRangeLimits(m.matPropertyList[i].__propIndex);
                            break;

                        case ShaderPropertyType.Vector:
                            backupValue.vector4 = currentValue.vector4 =
                                material.GetVector(currentInfo.propName);
                            break;

                        case ShaderPropertyType.Color:
                            backupValue.color = currentValue.color =
                                material.GetColor(currentInfo.propName);
                            break;
                    }
                }
            }
            #endregion
            /************************************************************************
             *                               Drawing Methods
             ************************************************************************/
            #region .
            private void DrawDefaultFields()
            {
                RitoEditorGUI.FoldoutHeaderBox(ref m.__optionFoldout1, EngHan("Options", "설정"), 6);
                if (!m.__optionFoldout1) return;

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Effect Material", "이펙트 마테리얼"));

                    EditorGUI.BeginChangeCheck();
                    m.effectMaterial = EditorGUILayout.ObjectField(m.effectMaterial, typeof(Material), false) as Material;
                    if (EditorGUI.EndChangeCheck())
                    {
                        isMaterialChanged = true;

                        //Debug.Log("Material Changed");

                        // 복제
                        if (m.effectMaterial != null)
                            m.effectMaterial = new Material(m.effectMaterial);
                    }

                    // 마테리얼 재할당
                    if (RitoEditorGUI.DrawButtonLayout("Reload", Color.white, Color.black, 60f))
                    {
                        if (m.effectMaterial != null)
                        {
                            //m.effectMaterial = new Material(m.effectMaterial);
                            InitMaterial();
                        }
                    }
                }

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Show Material Name", "마테리얼 이름 표시"));
                    m.showMaterialNameInHierarchy = EditorGUILayout.Toggle(m.showMaterialNameInHierarchy);
                }

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Priority", "우선순위"));
                    m.priority = EditorGUILayout.IntSlider(m.priority, -10, 10);
                }

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Time Mode", "시간 계산 방식"));

                    EditorGUI.BeginChangeCheck();

                    int selected = EditorGUILayout.Popup(m.isTimeModeSeconds ? 0 : 1, isHangle ? TimeModesHan : TimeModesEng);
                    m.isTimeModeSeconds = selected == 0 ? true : false;

                    // 시간 계산 방식 변경 시, 초 <-> 프레임 간 변환 적용
                    if (EditorGUI.EndChangeCheck())
                    {
                        // 프레임 -> 초로 바꾼 경우
                        if (m.isTimeModeSeconds)
                        {
                            m.durationSeconds = m.durationFrame * ReferenceSPF;

                            for (int i = 0; i < m.matPropertyList.Count; i++)
                            {
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].__HasEvents == false)
                                    continue;

                                var eventList = m.matPropertyList[i].eventList;
                                for (int j = 0; j < eventList.Count; j++)
                                {
                                    eventList[j].time = eventList[j].frame * ReferenceSPF;
                                }
                            }
                        }
                        // 초 -> 프레임으로 바꾼 경우
                        else
                        {
                            m.durationFrame = (int)(m.durationSeconds * ReferenceFPS);

                            for (int i = 0; i < m.matPropertyList.Count; i++)
                            {
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].__HasEvents == false)
                                    continue;

                                var eventList = m.matPropertyList[i].eventList;
                                for (int j = 0; j < eventList.Count; j++)
                                {
                                    eventList[j].frame = (int)(eventList[j].time * ReferenceFPS);
                                }
                            }
                        }
                    }
                }

                isDurationZero = false;

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(m.isTimeModeSeconds ?
                        EngHan("Duration Time", "지속 시간(초)") : EngHan("Duration Frame", "지속 시간(프레임)"));

                    // 1. 시간 계산 방식이 초 일경우
                    if (m.isTimeModeSeconds)
                    {
                        EditorGUI.BeginChangeCheck();

                        m.durationSeconds.RefClamp_000();
                        float prevDuration = m.durationSeconds;

                        Color col = GUI.color;
                        if (m.durationSeconds <= 0f)
                        {
                            GUI.color = Color.red * 2f;
                            isDurationZero = true;
                        }
                        m.durationSeconds = EditorGUILayout.FloatField(m.durationSeconds);
                        if (m.durationSeconds < 0f) m.durationSeconds = 0f;

                        GUI.color = col;

                        // Duration 변경 시, 비율을 유지하면서 이벤트들의 Time 변경
                        if (EditorGUI.EndChangeCheck() && m.durationSeconds > 0f)
                        {
                            float changeRatio = m.durationSeconds / prevDuration;

                            for (int i = 0; i < m.matPropertyList.Count; i++)
                            {
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].__HasEvents == false)
                                    continue;

                                var eventList = m.matPropertyList[i].eventList;
                                for (int j = 0; j < eventList.Count; j++)
                                {
                                    eventList[j].time *= changeRatio;
                                }
                            }
                        }
                    }
                    // 2. 시간 계산 방식이 프레임일 경우
                    else
                    {
                        EditorGUI.BeginChangeCheck();

                        int prevDurationFrame = m.durationFrame;

                        Color col = GUI.color;
                        if (m.durationFrame == 0)
                        {
                            GUI.color = Color.red * 2f;
                            isDurationZero = true;
                        }
                        m.durationFrame = EditorGUILayout.IntField(m.durationFrame);
                        if (m.durationFrame < 0) m.durationFrame = 0;

                        GUI.color = col;

                        // Duration 변경 시, 비율을 유지하면서 이벤트들의 Time 변경
                        if (EditorGUI.EndChangeCheck() && m.durationSeconds > 0f)
                        {
                            float changeRatio = (float)m.durationFrame / prevDurationFrame;

                            for (int i = 0; i < m.matPropertyList.Count; i++)
                            {
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].eventList == null || m.matPropertyList[i].eventList.Count == 0)
                                    continue;

                                var eventList = m.matPropertyList[i].eventList;
                                for (int j = 0; j < eventList.Count; j++)
                                {
                                    // 시작 이벤트 : 0프레임
                                    if (j == 0)
                                        continue;
                                    // 종료 이벤트 : 마지막 프레임
                                    else if (j == eventList.Count - 1)
                                        eventList[j].frame = m.durationFrame;
                                    // 나머지 이벤트 : 계산
                                    else
                                        eventList[j].frame = (int)(eventList[j].frame * changeRatio);
                                }
                            }
                        }
                    }
                }

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Stop Action", "종료 동작"));
                    if (isHangle)
                    {
                        m.stopAction = (StopAction)EditorGUILayout.Popup((int)m.stopAction, StopActionsHangle);
                    }
                    else
                    {
                        m.stopAction = (StopAction)EditorGUILayout.EnumPopup(m.stopAction);
                    }
                }

#if !UNITY_2019_3_OR_NEWER
                EditorGUILayout.Space();
#endif
            }

            private void DrawEditorOptions()
            {
                RitoEditorGUI.FoldoutHeaderBox(ref m.__optionFoldout2, EngHan("Editor Options", "에디터 기능"), 2);
                if (m.__optionFoldout2 == false)
                    return;

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Edit Mode", "편집 모드"));
                    m.__editMode = EditorGUILayout.Toggle(m.__editMode);
                }

                EditorGUI.BeginDisabledGroup(!m.__editMode);
                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    if (m.isTimeModeSeconds)
                        RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Current Time", "경과 시간"), m.__editMode ? TimeColor : Color.white);
                    else
                        RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Current Frane", "경과 프레임"), m.__editMode ? TimeColor : Color.white);

                    Color col = GUI.color;
                    if (m.__editMode)
                        GUI.color = TimeColor;

                    if (m.isTimeModeSeconds)
                        m.currentSeconds = EditorGUILayout.Slider(m.currentSeconds, 0f, m.durationSeconds);
                    else
                        m.currentFrame = EditorGUILayout.IntSlider(m.currentFrame, 0, m.durationFrame);

                    GUI.color = col;
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary> 현재 복제된 마테리얼의 수정 가능한 프로퍼티 목록 표시하기 </summary>
            private void DrawCopiedMaterialProperties()
            {
                RitoEditorGUI.FoldoutHeaderBox(ref m.__matPropListFoldout, EngHan("Material Properties", "마테리얼 프로퍼티 목록"),
                    m.matPropertyList.Count);

                if (!m.__matPropListFoldout)
                    return;

                EditorGUI.BeginDisabledGroup(isPlayMode && !m.__editMode);

                for (int i = 0; i < m.matPropertyList.Count; i++)
                {
                    var mp = m.matPropertyList[i];
                    if ((int)mp.propType == 4) // 4 : Texture
                        continue;

                    MaterialPropertyValue currentMatValue = m.__materialCurrentValues[i];

                    Color currentColor = mp.enabled ? EnabledColor : Color.gray;
                    bool hasEvents = mp.__HasEvents;

                    EditorGUILayout.BeginHorizontal();

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    RitoEditorGUI.DrawPrefixLabelLayout(mp.__displayName,
                        hasEvents ? currentColor : Color.white, 0.25f);

                    Color guiColor = GUI.color;
                    if (hasEvents)
                        GUI.color = currentColor;

                    switch (mp.propType)
                    {
                        case ShaderPropertyType.Float:
                            {
                                EditorGUI.BeginChangeCheck();

                                currentMatValue.floatValue = material.GetFloat(mp.propName);
                                currentMatValue.floatValue = EditorGUILayout.FloatField(currentMatValue.floatValue);

                                if (EditorGUI.EndChangeCheck())
                                    material.SetFloat(mp.propName, currentMatValue.floatValue);
                            }
                            break;
                        case ShaderPropertyType.Range:
                            {
                                EditorGUI.BeginChangeCheck();

                                currentMatValue.floatValue = material.GetFloat(mp.propName);
                                currentMatValue.floatValue =
                                    EditorGUILayout.Slider(currentMatValue.floatValue, currentMatValue.min, currentMatValue.max);

                                if (EditorGUI.EndChangeCheck())
                                    material.SetFloat(mp.propName, currentMatValue.floatValue);
                            }
                            break;
                        case ShaderPropertyType.Vector:
                            {
                                EditorGUI.BeginChangeCheck();

                                currentMatValue.vector4 = material.GetVector(mp.propName);
                                currentMatValue.vector4 =
                                    EditorGUILayout.Vector4Field("", currentMatValue.vector4);

                                if (EditorGUI.EndChangeCheck())
                                    material.SetVector(mp.propName, currentMatValue.vector4);
                            }
                            break;
                        case ShaderPropertyType.Color:
                            {
                                EditorGUI.BeginChangeCheck();

                                currentMatValue.color = material.GetColor(mp.propName);
                                currentMatValue.color =
                                    EditorGUILayout.ColorField(currentMatValue.color);

                                if (EditorGUI.EndChangeCheck())
                                    material.SetColor(mp.propName, currentMatValue.color);
                            }
                            break;
                    }

                    GUI.color = guiColor;

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    // 각 프로퍼티마다 리셋 버튼 - 클릭 시 백업된 기본값으로 마테리얼 프로퍼티 값 변경
                    if (RitoEditorGUI.DrawButtonLayout("R", Color.magenta, 20f, 18f))
                    {
                        switch (mp.propType)
                        {
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range:
                                m.__materialCurrentValues[i].floatValue = m.__materialDefaultValues[i].floatValue;
                                material.SetFloat(mp.propName, m.__materialDefaultValues[i].floatValue);
                                break;

                            case ShaderPropertyType.Vector:
                                m.__materialCurrentValues[i].vector4 = m.__materialDefaultValues[i].vector4;
                                material.SetVector(mp.propName, m.__materialDefaultValues[i].vector4);
                                break;

                            case ShaderPropertyType.Color:
                                m.__materialCurrentValues[i].color = m.__materialDefaultValues[i].color;
                                material.SetColor(mp.propName, m.__materialDefaultValues[i].color);
                                break;
                        }
                    }

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    // 프로퍼티에 이벤트 존재할 경우 : 활성화, 제거 버튼
                    if (hasEvents)
                    {
                        string enableButtonString = mp.enabled ? "E" : "D";

                        if (RitoEditorGUI.DrawButtonLayout(enableButtonString, currentColor, 22f, 18f))
                            mp.enabled = !mp.enabled;

                        if (RitoEditorGUI.DrawButtonLayout("-", Color.red * 1.5f, 20f, 18f))
                            mp.Edt_RemoveAllEvents();
                    }
                    // 이벤트 없을 경우 : 추가 버튼
                    else
                    {
#if UNITY_2017_9_OR_NEWER
                        const float PlusButtonWidth = 42f;
#else
                        const float PlusButtonWidth = 46f;
#endif
                        bool addButton = RitoEditorGUI.DrawButtonLayout("+", Color.green * 1.5f, PlusButtonWidth, 18f);
                        if (addButton)
                        {
                            mp.Edt_AddInitialEvents(m.isTimeModeSeconds ? m.durationSeconds : m.durationFrame, m.isTimeModeSeconds);
                        }
                    }

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.EndDisabledGroup();
            }

            /// <summary> 프로퍼티 이벤트들 모두 그리기 </summary>
            private void DrawMaterialPropertyEventList()
            {
                if (isDurationZero)
                {
                    EditorStyles.helpBox.fontSize = 12;
                    EditorGUILayout.HelpBox(
                        EngHan("Duration must be more than ZERO.", "지속 시간이 0일 경우 이벤트가 재생되지 않습니다."),
                        MessageType.Warning);
                    return;
                }

                for (int i = 0; i < m.matPropertyList.Count; i++)
                {
                    if (m.matPropertyList[i].__HasEvents)
                    {
                        DrawPropertyEvents(m.matPropertyList[i], () =>
                        {
                            // [-] 버튼 클릭하면 해당 프로퍼티에서 이벤트들 싹 제거
                            m.matPropertyList[i].Edt_RemoveAllEvents();
                        }
                        );

                        EditorGUILayout.Space();

                        if (m.matPropertyList[i].__foldout)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                        }
                    }
                }
            }


            GUIStyle eventFoldoutHeaderStyle;

            // 그래프 너비 옵션
            const float GraphToggleButtonWidth = 100f;
            const float GraphMarginLeft = 4f;
            const float GraphMarginRight = 4f;

            // 그래프 높이
            const float GraphMarginTop = 2f;           // 최상단 ~ 토글 버튼 사이 간격
            const float GraphToggleButtonHeight = 20f; // 토글 버튼 높이
            const float GraphMarginCenter = 2f;        // 토글 버튼 ~ 그래프 사이 간격
            const float GraphHeight = 80f;             // 그래프 높이
            const float GraphMarginBottom = 8f;        // 그래프 하단 간격

            const float GraphTimestampHeightOnTop = 20f; // 그래프 상단 현재 시간 표시

            /// <summary> 프로퍼티 하나의 이벤트 모두 그리기 </summary>
            private void DrawPropertyEvents(MaterialPropertyInfo mp, Action removeAction)
            {
                ref bool enabled = ref mp.enabled;
                bool isColorType = mp.propType == ShaderPropertyType.Color;
                bool isVectorType = mp.propType == ShaderPropertyType.Vector;

                // 그래프 그릴지 여부 확인
                bool showGraph = mp.__showGraphFloat;
                for (int i = 0; i < mp.__showGraphVector.Length; i++)
                    showGraph |= mp.__showGraphVector[i];

                // 그래프 최종 높이
                float graphTotalHeight = GraphMarginTop + GraphToggleButtonHeight + GraphMarginCenter;

                if (showGraph)
                {
                    graphTotalHeight += GraphHeight + GraphMarginBottom;

                    if (isPlayMode)
                        graphTotalHeight += GraphTimestampHeightOnTop; // 그래프 상단
                }

                // 이벤트 하나당 요소 개수
                int countPerEvent = isColorType ? 3 : 2;

                // 전체 이벤트의 요소 개수
                int contentCount = countPerEvent * mp.eventList.Count;

                // 전체 이벤트의 + 버튼 개수
                int plusButtonCount = mp.eventList.Count - 1;

#if UNITY_2019_3_OR_NEWER
                float heightPerElement = isVectorType ? 22f : 21f;
                float heightPerButton = isColorType ? 23f : 22f;
#else
                float heightPerElement = isVectorType ? 21f : 21f;
                float heightPerButton = isColorType ? 17f : 20f;
#endif

                // 최종 Foldout 높이 결정
                float foldoutContHeight = graphTotalHeight + contentCount * heightPerElement + plusButtonCount * heightPerButton;

                // Foldout 스타일 설정
                if (eventFoldoutHeaderStyle == null)
                    eventFoldoutHeaderStyle = new GUIStyle(EditorStyles.label);
                eventFoldoutHeaderStyle.normal.textColor =
                eventFoldoutHeaderStyle.onNormal.textColor = enabled ? Color.black : Color.gray;
                eventFoldoutHeaderStyle.fontStyle = FontStyle.Bold;

                string headerText = $"{mp.__displayName} [{mp.propType}]";

                Color headerBgColor = enabled ? new Color(0f, 0.8f, 0.8f) : new Color(0.1f, 0.1f, 0.1f);
                Color enableButtonTextColor = enabled ? Color.white : Color.white;
                Color enableButtonBgColor = enabled ? Color.black : Color.gray;
                Color removeButtonColor = Color.red * 1.8f;

                // Draw Foldout
                RitoEditorGUI.EventFoldoutHeaderBox(ref mp.__foldout, headerText, foldoutContHeight, eventFoldoutHeaderStyle,
                    headerBgColor, enableButtonBgColor, enableButtonTextColor, removeButtonColor,
                    ref mp.enabled, out bool removePressed);

                if (removePressed)
                {
                    removeAction();
                }

                if (!mp.__foldout) return;
                // ======================= Foldout 펼쳐져 있을 때 ======================= //

                // == 그래프 그리기 ==

                // [1] 상단 여백
                GUILayoutUtility.GetRect(1f, GraphMarginTop);

                // [2] 토글 버튼
                Rect graphBtnRect = GUILayoutUtility.GetRect(1f, GraphToggleButtonHeight);

                // [3] 버튼 ~ 그래프 사이 여백
                GUILayoutUtility.GetRect(1f, GraphMarginCenter);

                Rect graphRect = default;

                // == 그래프 표시 허용 상태 ==
                if (showGraph)
                {
                    // [4] 그래프 상단 시간 표시
                    if (isPlayMode)
                    {
                        Rect graphTimeRect = GUILayoutUtility.GetRect(1f, GraphTimestampHeightOnTop);
                        DrawTimestampOverGraph(graphTimeRect);
                    }

                    // [5] 그래프 위치 확보
                    graphRect = GUILayoutUtility.GetRect(1f, GraphHeight);
                    graphRect.xMin += GraphMarginLeft;
                    graphRect.xMax -= GraphMarginRight;

                    // [6] 하단 여백
                    GUILayoutUtility.GetRect(1f, GraphMarginBottom);

                    // [7] 그래프 영역 마우스 이벤트 처리
                    //  - 그래프 클릭 방지
                    //  - 플레이 & 편집 모드 => 진행도 설정
                    HandleMouseEventInGraphRect(graphRect);
                }

                // [8] 토글 버튼 & 그래프 그리기
                if (mp.propType == ShaderPropertyType.Float || mp.propType == ShaderPropertyType.Range)
                    DrawFloatGraph(mp, graphBtnRect, graphRect);
                else
                    DrawVector4OrColorGraph(mp, graphBtnRect, graphRect);

                if (showGraph)
                {
                    // [8] 그래프에 X 좌표마다 강조 표시
                    //  - 현재 등록된 이벤트들 위치
                    //  - 현재 재생 중인 위치
                    DrawMarkersOnGraphRect(mp, graphRect);
                }

                int addNewEvent = -1;

                var eventList = mp.eventList;
                for (int i = 0; i < eventList.Count; i++)
                {
                    // 이벤트 항목 한개 그리기
                    DrawEachEvent(mp, eventList[i], i);

                    // 이벤틑 사이사이 [+] 버튼 : 새로운 이벤트 추가
                    if (i < eventList.Count - 1)
                    {
                        EditorGUILayout.BeginHorizontal();

                        const float ButtonWidth = 24f;

                        // 버튼 중앙 정렬
                        RitoEditorGUI.DrawHorizontalSpace((EditorGUIUtility.currentViewWidth) * 0.5f - ButtonWidth);
                        if (RitoEditorGUI.DrawPlusButtonLayout(ButtonWidth)) addNewEvent = i;

                        EditorGUILayout.EndHorizontal();
                    }
                }

                // 새로운 이벤트 추가
                if (addNewEvent > -1)
                {
                    mp.Edt_AddNewEvent(addNewEvent, m.isTimeModeSeconds);
                }
            }

            /// <summary> 그래프 내 마우스 이벤트 처리 </summary>
            private void HandleMouseEventInGraphRect(in Rect graphRect)
            {
                Event current = Event.current;
                Vector2 mPos = current.mousePosition;

                if (graphRect.Contains(mPos) && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
                {
                    // 편집 모드일 경우, 마우스 클릭 좌표에 따라 진행도 변경
                    if (isPlayMode && m.__editMode)
                    {
                        // X : 0. ~ 1.
                        float ratio = (mPos.x - graphRect.x) / graphRect.width;

                        // 진행도 변경
                        if (m.isTimeModeSeconds)
                            m.currentSeconds = m.durationSeconds * ratio;
                        else
                            m.currentFrame = (int)(m.durationFrame * ratio);
                    }

                    // 그래프 마우스 클릭 방지
                    current.Use(); // Set Handled
                }
            }

            /// <summary> 그래프 상단에 현재 시간 또는 프레임 표시 </summary>
            private void DrawTimestampOverGraph(Rect graphTimeRect)
            {
                graphTimeRect.xMin += GraphMarginLeft;
                graphTimeRect.xMax -= GraphMarginRight;

                float totalWidth = graphTimeRect.width;
                float xBegin = graphTimeRect.x;

                const float XPosAdjustment = 11f;
#if UNITY_2019_3_OR_NEWER
                const float XPosClampRight = 28f;
#else
                const float XPosClampRight = 32f;
#endif

                float xMin = xBegin + (currentTimeOrFrameRatio * totalWidth) - XPosAdjustment;
                xMin = Mathf.Max(xBegin, xMin);                              // Clamp Left : xBegin
                xMin = Mathf.Min(xMin, graphTimeRect.xMax - XPosClampRight); // Clamp Right

                graphTimeRect.xMin = xMin;

                if (m.isTimeModeSeconds)
                    EditorGUI.LabelField(graphTimeRect, $"{m.currentSeconds:F2}", yellowBoldLabelStyle);
                else
                    EditorGUI.LabelField(graphTimeRect, $"{m.currentFrame}", yellowBoldLabelStyle);
            }

            /// <summary> 그래프 내의 특정 위치들을 강조 표시하기 </summary>
            private void DrawMarkersOnGraphRect(MaterialPropertyInfo mp, in Rect graphRect)
            {
                Rect markerRect = new Rect(graphRect);
                float baseXPos = graphRect.x;
                float totalWidth = graphRect.width - 2f;
                markerRect.width = 2f;

                // 1. 이벤트 위치들 표시
                var eventList = mp.eventList;
                for (int i = 1; i < eventList.Count - 1; i++)
                {
                    Rect r = new Rect(markerRect);
                    var e = eventList[i];

                    float ratio = m.isTimeModeSeconds ?
                        e.time / m.durationSeconds :
                        (float)e.frame / m.durationFrame;

                    r.x = baseXPos + (ratio * totalWidth);

                    EditorGUI.DrawRect(r, new Color(1, 1, 1, 0.2f)); // 마커 그리기
                }

                if (isPlayMode)
                {
                    // 2. 그래프에 현재 재생 중인 위치 표시하기
                    if (m.isTimeModeSeconds && m.durationSeconds > 0f ||
                        !m.isTimeModeSeconds && m.durationFrame > 0)
                    {
                        Rect currentPlayingRect = new Rect(markerRect);

                        currentPlayingRect.x = baseXPos + (currentTimeOrFrameRatio * totalWidth);

                        EditorGUI.DrawRect(currentPlayingRect, Color.yellow);
                    }
                }
            }

            /// <summary> Float, Range 그래프를 그림미당 </summary>
            private void DrawFloatGraph(MaterialPropertyInfo mp, Rect buttonRect, in Rect graphRect)
            {
                // Note : buttonRect는 여백 설정되지 않은 영역(중앙 정렬 필요)
                //        graphRect는 여백 설정 모두 완료(바로 사용)

                // 1. 토글 버튼
                float viewWidth = buttonRect.width;
                buttonRect.width = GraphToggleButtonWidth;
                buttonRect.x = (viewWidth - GraphToggleButtonWidth * 0.5f) * 0.5f;

                string buttonLabel = mp.__showGraphFloat ?
                    EngHan("Hide Graph", "그래프 숨기기") :
                    EngHan("Show Graph", "그래프 표시");

                Color buttonColor = mp.__showGraphFloat ? Color.white * 2f : Color.black;
                whiteTextButtonStyle.normal.textColor = mp.__showGraphFloat ? Color.black : Color.white;
                whiteTextButtonStyle.hover.textColor = Color.gray;

                // 그래프 표시 토글 버튼
                if (RitoEditorGUI.DrawButton(buttonRect, buttonLabel, buttonColor, whiteTextButtonStyle))
                {
                    mp.__showGraphFloat = !mp.__showGraphFloat;
                }
                if (mp.__showGraphFloat == false)
                    return;

                // 2. 그래프
                AnimationCurve graph = new AnimationCurve();
                for (int i = 0; i < mp.eventList.Count; i++)
                {
                    var curValue = mp.eventList[i];

                    float t = m.isTimeModeSeconds ? curValue.time : (float)curValue.frame;

                    // 인접한 두 키의 시간이 동일한 경우, 시간을 미세하게 더해주기
                    if (0 < i && i < mp.eventList.Count)
                    {
                        if (curValue.time == mp.eventList[i - 1].time)
                            t += 0.001f;
                    }

                    graph.AddKey(t, curValue.floatValue);
                }
                for (int i = 0; i < graph.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(graph, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(graph, i, AnimationUtility.TangentMode.Linear);
                }

                // 그래프 배경 색상 설정
                fiCurveBGColor.SetValue(null, new Color(0.15f, 0.15f, 0.15f));

                // 그래프 그리기
                EditorGUI.CurveField(graphRect, graph);

                // 그래프 배경 색상 복원
                fiCurveBGColor.SetValue(null, defaultCurveBGColor);
            }

            private static string[] rgbaButtonLabels;
            private static Color[] rgbaSignatureColors;
            private static readonly Color rgbaButtonDisabledColor = Color.black;

            private static FieldInfo fiCurveBGColor;
            private static Color defaultCurveBGColor;

            /// <summary> Vector4, Color 그래프를 그림미당 </summary>
            private void DrawVector4OrColorGraph(MaterialPropertyInfo mp, in Rect buttonRect, in Rect graphRect)
            {
                const int ButtonCount = 4;

                // true : Vector4, false : Color
                bool isVectorOrColorType = mp.propType == ShaderPropertyType.Vector;

                // Init(최초 한 번씩 실행)
                {
                    if (rgbaButtonLabels == null)
                    {
                        rgbaButtonLabels = isVectorOrColorType ?
                            new string[ButtonCount] { "X", "Y", "Z", "W" } :
                            new string[ButtonCount] { "R", "G", "B", "A" };
                    }
                    if (rgbaSignatureColors == null)
                    {
                        rgbaSignatureColors = new Color[ButtonCount]
                        {
                            Color.red,
                            Color.green,
                            Color.blue,
                            Color.white,
                        };
                    }
                }

                // 1. 토글 버튼
                const float ButtonWidth = 20f;
                const float Margin = 4f;
                float centerX = buttonRect.width * 0.5f;

                Rect[] buttonRects = new Rect[ButtonCount];
                buttonRects[0] = new Rect(buttonRect);
                buttonRects[0].width = ButtonWidth;
                buttonRects[0].x = centerX - (ButtonWidth);

                // 모든 버튼 Rect 초기화
                for (int i = 1; i < ButtonCount; i++)
                {
                    buttonRects[i] = new Rect(buttonRects[0]);
                    buttonRects[i].x += i * (ButtonWidth + Margin);
                }

                // 토글 버튼 그리기
                for (int i = 0; i < ButtonCount; i++)
                {
                    Color buttonColor = mp.__showGraphVector[i] ? rgbaSignatureColors[i] * 2f : rgbaButtonDisabledColor;

                    whiteTextButtonStyle.normal.textColor = mp.__showGraphVector[i] ? Color.black : Color.white;
                    whiteTextButtonStyle.hover.textColor = Color.gray;
                    if (RitoEditorGUI.DrawButton(buttonRects[i], rgbaButtonLabels[i], buttonColor, whiteTextButtonStyle))
                    {
                        mp.__showGraphVector[i] = !mp.__showGraphVector[i];
                    }
                }

                // 그래프 보일지 여부 결정
                bool drawGraph = false;
                for (int i = 0; i < ButtonCount; i++)
                    drawGraph |= mp.__showGraphVector[i];

                if (drawGraph == false)
                    return;

                // 2. 그래프
                AnimationCurve[] graphs = new AnimationCurve[ButtonCount];

                // ====== Multiple Curves =======
                // 다중 커브를 그리기 위해 커브 배경 색상 투명화
                fiCurveBGColor.SetValue(null, Color.clear);

                // 그래프 배경 색상
                EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

                // 그래프 마우스 클릭 방지
                if (graphRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.MouseDown)
                        Event.current.Use();
                }

                // 그래프의 Y축 최솟값 구하기
                float graphMinY = float.MaxValue;
                float graphMaxY = float.MinValue;

                // 벡터
                if (isVectorOrColorType)
                {
                    for (int i = 0; i < ButtonCount; i++)
                    {
                        if (mp.__showGraphVector[i] == false)
                            continue;

                        for (int j = 0; j < mp.eventList.Count; j++)
                        {
                            if (graphMinY > mp.eventList[j].vector4[i])
                                graphMinY = mp.eventList[j].vector4[i];
                            if (graphMaxY < mp.eventList[j].vector4[i])
                                graphMaxY = mp.eventList[j].vector4[i];
                        }
                    }
                }
                // 컬러
                else
                {
                    graphMinY = 0f;
                    for (int i = 0; i < ButtonCount; i++)
                    {
                        if (mp.__showGraphVector[i] == false)
                            continue;

                        for (int j = 0; j < mp.eventList.Count; j++)
                        {
                            if (graphMaxY < mp.eventList[j].vector4[i])
                                graphMaxY = mp.eventList[j].vector4[i];
                        }
                    }

                    if (graphMaxY < 1f)
                        graphMaxY = 1f;
                }

                // a : 0 ~ 3 (X Y Z W 또는 R G B A)
                for (int a = 0; a < ButtonCount; a++)
                {
                    AnimationCurve graph = graphs[a] = new AnimationCurve();

                    // 토글 ON인 경우만 그래프 그리기
                    if (mp.__showGraphVector[a] == false)
                        continue;

                    // i : 이벤트 개수
                    for (int i = 0; i < mp.eventList.Count; i++)
                    {
                        var current = mp.eventList[i];

                        float t = m.isTimeModeSeconds ? current.time : (float)current.frame;

                        // 인접한 두 키의 시간이 동일한 경우, 시간을 미세하게 더해주기
                        if (0 < i && i < mp.eventList.Count)
                        {
                            if (current.time == mp.eventList[i - 1].time)
                                t += 0.001f;
                        }

                        graph.AddKey(t, current.vector4[a]);
                    }
                    for (int i = 0; i < graph.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(graph, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(graph, i, AnimationUtility.TangentMode.Linear);
                    }

                    // 겹친 그래프들의 상하 잘림 방지
                    Rect gr = new Rect(graphRect);
                    gr.height += a;

                    float curveWidth = m.isTimeModeSeconds ? m.durationSeconds : m.durationFrame;
                    float curveHeight = graphMaxY - graphMinY;
                    if (curveHeight == 0f)
                        curveHeight = 1f;

                    EditorGUI.CurveField(gr, graph, rgbaSignatureColors[a], new Rect(0, graphMinY, curveWidth, curveHeight));
                }

                // 그래프 배경 색상 돌려놓기
                fiCurveBGColor.SetValue(null, defaultCurveBGColor);
            }

            private static readonly Color HighlightBasic = new Color(0.1f, 0.1f, 0.1f);
            private static readonly Color HighlightFirstOrLast = new Color(0.3f, 0.3f, 0.3f);
            private static readonly Color HighlightPlaying = new Color(0.0f, 0.4f, 0.5f);

            /// <summary> 프로퍼티의 이벤트 하나 그리기 (시간, 값) </summary>
            private void DrawEachEvent(MaterialPropertyInfo mp, MaterialPropertyValue mpEvent, int index)
            {
                bool isFirst = index == 0;
                bool isLast = index == mp.eventList.Count - 1;
                bool isFirstOrLast = isFirst || isLast;
                bool isColorType = mp.propType == ShaderPropertyType.Color;

                // Clamp Time Value (First, Last)
                if (isFirst) mpEvent.time = 0f;
                else if (isLast) mpEvent.time = m.durationSeconds;

                // 현재 재생, 보간되는 두 이벤트 배경 하이라이트
                bool currentPlaying =
                    isPlayMode &&
                    m.isActiveAndEnabled &&
                    mp.enabled &&
                    (index == mp.__playingIndex || index - 1 == mp.__playingIndex);

                // 추가된 이벤트마다 배경 하이라이트
                Rect highlightRight = GUILayoutUtility.GetRect(1f, 0f);
#if UNITY_2019_3_OR_NEWER
                highlightRight.height = isColorType ? 62f : 42f;
#else
                highlightRight.height = isColorType ? 58f : 40f;
#endif
                highlightRight.xMin += 4f;
                highlightRight.xMax -= 4f;

                Rect highlightLeft = new Rect(highlightRight);
                highlightLeft.xMax = 40f;
                highlightRight.xMin += 24f;

                if (currentPlaying)
                {
                    EditorGUI.DrawRect(highlightLeft, currentPlaying ? HighlightPlaying : HighlightBasic);
                    EditorGUI.DrawRect(highlightRight, currentPlaying ? HighlightPlaying : HighlightBasic);
                }
                else
                {
                    if (isFirstOrLast)
                    {
                        EditorGUI.DrawRect(highlightLeft, HighlightFirstOrLast);
                        EditorGUI.DrawRect(highlightRight, HighlightFirstOrLast);
                    }
                    else
                    {
                        EditorGUI.DrawRect(highlightLeft, HighlightBasic);
                        EditorGUI.DrawRect(highlightRight, HighlightBasic);
                    }
                }


                const float LeftMargin = 6f;
                const float IndexLabelWidth = 20f;
                const float LabelWidth = 80f;
                const float MinusButtonWidth = 40f;
                const float RightButtonMargin = 6f;

                // 1. Time 슬라이더
                if (isFirstOrLast) EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.BeginHorizontal();
                {
#if UNITY_2019_3_OR_NEWER
                    float LM = index > 9 ? 4f : 0f;
#else
                    float LM = index > 9 ? 6f : 0f;
#endif
                    RitoEditorGUI.DrawHorizontalSpace(LeftMargin - LM);

                    Rect indexRect = GUILayoutUtility.GetRect(IndexLabelWidth + LM, 18f, whiteBoldLabelStyle);

                    // 좌측 인덱스 레이블의 높이 조정
#if UNITY_2019_3_OR_NEWER
                    indexRect.y += isColorType ? 18f : 10f;
#else
                    indexRect.y += isColorType ? 14f : 6f;
#endif

                    // 좌측 인덱스(숫자) 레이블
                    EditorGUI.LabelField(indexRect, index.ToString(), whiteBoldLabelStyle);

                    // 시간 레이블
                    string timeLabel;
                    if (m.isTimeModeSeconds)
                    {
                        timeLabel =
                            isFirst ? EngHan("Begin", "시작 시간") :
                            isLast ? EngHan("End", "종료 시간") :
                            EngHan("Time", "시간");
                    }
                    else
                    {
                        timeLabel =
                            isFirst ? EngHan("Begin", "시작 프레임") :
                            isLast ? EngHan("End", "종료 프레임") :
                            EngHan("Time", "프레임");
                    }
                    RitoEditorGUI.DrawPrefixLabelLayout(timeLabel, isFirstOrLast ? Color.white : TimeColor, LabelWidth, true);

                    // 시간 슬라이더
                    Color guiColor = GUI.color;
                    if (isFirstOrLast == false)
                        GUI.color = TimeColor;

                    // [1] 시간 계산 방식 : 초
                    if (m.isTimeModeSeconds)
                    {
                        mpEvent.time.RefClamp_000();

                        EditorGUI.BeginChangeCheck();
                        mpEvent.time = EditorGUILayout.Slider(mpEvent.time, 0f, m.durationSeconds);
                        if (EditorGUI.EndChangeCheck())
                        {
                            MaterialPropertyValue prevEvent = mp.eventList[index - 1];
                            MaterialPropertyValue nextEvent = mp.eventList[index + 1];

                            if (mpEvent.time < prevEvent.time)
                                mpEvent.time = prevEvent.time;
                            if (mpEvent.time > nextEvent.time)
                                mpEvent.time = nextEvent.time;
                        }
                    }
                    // [2] 시간 계산 방식 : 프레임
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        mpEvent.frame = EditorGUILayout.IntSlider(mpEvent.frame, 0, m.durationFrame);
                        if (EditorGUI.EndChangeCheck())
                        {
                            MaterialPropertyValue prevEvent = mp.eventList[index - 1];
                            MaterialPropertyValue nextEvent = mp.eventList[index + 1];

                            if (mpEvent.frame < prevEvent.frame)
                                mpEvent.frame = prevEvent.frame;
                            if (mpEvent.frame > nextEvent.frame)
                                mpEvent.frame = nextEvent.frame;
                        }
                    }

                    GUI.color = guiColor;

                    // 값 변경 시, 전후값의 경계에서 전후값 변경
                    // *ISSUE : 키보드로 직접 값 수정할 때, 도중에 전후값이 수정되는 문제 발생

                    //if (isFirstOrLast == false)
                    //{
                    //    MaterialPropertyValue prevEvent = mp.eventList[index - 1];
                    //    MaterialPropertyValue nextEvent = mp.eventList[index + 1];

                    //    if (m.isTimeModeSeconds)
                    //    {
                    //        if (prevEvent.time > mpEvent.time)
                    //            prevEvent.time = mpEvent.time;
                    //        if (nextEvent.time < mpEvent.time)
                    //            nextEvent.time = mpEvent.time;
                    //    }
                    //    else
                    //    {
                    //        if (prevEvent.frame > mpEvent.frame)
                    //            prevEvent.frame = mpEvent.frame;
                    //        if (nextEvent.frame < mpEvent.frame)
                    //            nextEvent.frame = mpEvent.frame;
                    //    }
                    //}

                    // 여백 생성
                    RitoEditorGUI.DrawHorizontalSpace(MinusButtonWidth);
                    Rect minusButtonRect = GUILayoutUtility.GetLastRect();
                    minusButtonRect.xMax -= RightButtonMargin;

                    // 이 이벤트 제거 버튼
                    if (isFirstOrLast == false)
                    {
                        if (RitoEditorGUI.DrawButton(minusButtonRect, "-", MinusButtonColor, bigMinusButtonStyle))
                            mp.eventList.RemoveAt(index);
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (isFirstOrLast) EditorGUI.EndDisabledGroup();


                // 2. 값 그리기
                EditorGUILayout.BeginHorizontal();

                RitoEditorGUI.DrawHorizontalSpace(LeftMargin);
                RitoEditorGUI.DrawHorizontalSpace(IndexLabelWidth); // 0, 1, 2, ... -> 인덱스 레이블 영역

                RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Value", "값"), Color.white, LabelWidth, true);

                Color col = GUI.color;
                GUI.color = Color.white * 2f;

                switch (mp.propType)
                {
                    case ShaderPropertyType.Float:
                        mpEvent.floatValue = EditorGUILayout.FloatField(mpEvent.floatValue);
                        break;

                    case ShaderPropertyType.Range:
                        mpEvent.floatValue = EditorGUILayout.Slider(mpEvent.floatValue, mpEvent.min, mpEvent.max);
                        break;

                    case ShaderPropertyType.Vector:
                        // Vector4의 레이블 X Y Z W를 하얗게
                        Color colLN = EditorStyles.label.normal.textColor;
                        EditorStyles.label.normal.textColor = Color.white;
                        {
                            mpEvent.vector4 = EditorGUILayout.Vector4Field("", mpEvent.vector4); // Vec4 Field
                        }
                        EditorStyles.label.normal.textColor = colLN;
                        break;

                    case ShaderPropertyType.Color:
                        EditorGUILayout.BeginVertical();

                        mpEvent.vector4.RefClamp_000();

                        mpEvent.color = EditorGUILayout.ColorField(mpEvent.color); // Color Field

                        Color colLN2 = EditorStyles.label.normal.textColor;
                        EditorStyles.label.normal.textColor = Color.white;
                        {
                            mpEvent.vector4 = EditorGUILayout.Vector4Field("", mpEvent.vector4); // Vec4 Field
                        }
                        EditorStyles.label.normal.textColor = colLN2;

                        EditorGUILayout.EndVertical();
                        break;
                }

                GUI.color = col;

                RitoEditorGUI.DrawHorizontalSpace(MinusButtonWidth);
                Rect setButtonRect = GUILayoutUtility.GetLastRect();
                setButtonRect.xMax -= RightButtonMargin;

                // Set 버튼 : 현재 마테리얼이 가진 값으로 값 설정
                if (RitoEditorGUI.DrawButton(setButtonRect, "Set", Color.magenta * 1.5f))
                {
                    switch (mp.propType)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            mpEvent.floatValue = material.GetFloat(mp.propName);
                            break;
                        case ShaderPropertyType.Vector:
                            mpEvent.vector4 = material.GetVector(mp.propName);
                            break;
                        case ShaderPropertyType.Color:
                            mpEvent.color = material.GetColor(mp.propName);
                            break;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            #endregion
        }
#endif
        #endregion
        /***********************************************************************
        *                           Custom EditorGUI
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        private static class RitoEditorGUI
        {
            public static readonly Color defaultHeaderBoxColor = new Color(0.1f, 0.1f, 0.1f);
            public static readonly Color defaultContentBoxColor = new Color(0.25f, 0.25f, 0.25f);
            public static readonly Color defaultHeaderTextColor = Color.white;
            public static readonly Color defaultOutlineColor = Color.black;

            public static Color PlusButtonColor { get; set; } = Color.green * 1.5f;
            public static Color MinusButtonColor { get; set; } = Color.red * 1.5f;
            public static Color HeaderBoxColor { get; set; } = defaultHeaderBoxColor;
            public static Color ContentBoxColor { get; set; } = defaultContentBoxColor;
            public static Color HeaderTextColor { get; set; } = defaultHeaderTextColor;
            public static Color OutlineColor { get; set; } = defaultOutlineColor;

            public static Color PrefixLabelColor { get; set; } = Color.white;

            private static GUIStyle prefixLabelStyle;
            public static void DrawPrefixLabelLayout(string label, in Color color = default, float width = 0.36f, bool fixedWidth = false)
            {
                if (prefixLabelStyle == null)
                    prefixLabelStyle = new GUIStyle(EditorStyles.label);
                prefixLabelStyle.normal.textColor = color == default ? PrefixLabelColor : color;

                if (!fixedWidth)
                    width = EditorGUIUtility.currentViewWidth * width;

                EditorGUILayout.LabelField(label, prefixLabelStyle, GUILayout.Width(width));
            }
            public static bool DrawButtonLayout(string label, in Color buttonColor, in float width, in float height = 20f)
            {
                Color bCol = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;

                bool pressed = GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height));

                GUI.backgroundColor = bCol;
                return pressed;
            }
            public static bool DrawButtonLayout(string label, in Color textColor, in Color buttonColor, in float width)
            {
                Color bCol = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;

                GUIStyle buttonStyle = new GUIStyle("button");
                buttonStyle.normal.textColor = textColor;

                bool pressed = GUILayout.Button(label, buttonStyle, GUILayout.Width(width));

                GUI.backgroundColor = bCol;
                return pressed;
            }
            public static bool DrawButton(in Rect rect, string label, in Color buttonColor, GUIStyle style = null)
            {
                Color bCol = GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;

                bool pressed = style != null ? GUI.Button(rect, label, style) : GUI.Button(rect, label);

                GUI.backgroundColor = bCol;
                return pressed;
            }
            public static void DrawHorizontalSpace(float width)
            {
                EditorGUILayout.LabelField("", GUILayout.Width(width));
            }
            public static bool DrawPlusButtonLayout(in float width = 40f)
            {
                return DrawButtonLayout("+", PlusButtonColor, width);
            }
            public static bool DrawMinusButtonLayout(in float width = 40f)
            {
                return DrawButtonLayout("-", MinusButtonColor, width);
            }

            static GUIStyle foldoutHeaderTextStyle;
            public static void FoldoutHeaderBox(ref bool foldout, string headerText, int contentCount, bool setDefaultColors = true)
            {
                if (setDefaultColors)
                {
                    HeaderBoxColor = defaultHeaderBoxColor;
                    ContentBoxColor = defaultContentBoxColor;
                    HeaderTextColor = defaultHeaderTextColor;
                    OutlineColor = defaultOutlineColor;
                }

                const float OutWidth = 2f;
                const float HeaderHeight = 20f;
                const float OneHeight = 20f;
                const float HeaderLeftPadding = 4f; // 헤더 박스 내 좌측 패딩(레이블 왼쪽 여백)
                const float ContentTopPadding = 4f; // 내용 박스 내 상단 패딩
                const float ContentBotPadding = 4f; // 내용 박스 내 하단 패딩
                float contentHeight = !foldout ? 0f : (ContentTopPadding + OneHeight * contentCount + ContentBotPadding);
                float totalHeight = !foldout ? (HeaderHeight) : (HeaderHeight + OutWidth + contentHeight);

                Rect H = GUILayoutUtility.GetRect(1, HeaderHeight); // Header
                GUILayoutUtility.GetRect(1f, ContentTopPadding); // Content Top Padding

                // Note : 가로 외곽선이 꼭짓점을 덮는다.

                Rect T = new Rect(); // Top
                T.y = H.y - OutWidth;
                T.height = OutWidth;
                T.xMin = H.xMin - OutWidth;
                T.xMax = H.xMax + OutWidth;

                Rect BH = new Rect(T); // Bottom of Header
                BH.y = H.yMax;

                Rect L = new Rect(); // Left
                L.x = H.x - OutWidth;
                L.y = H.y;
                L.width = OutWidth;
                L.height = totalHeight;

                Rect R = new Rect(L); // Right
                R.x = H.xMax;

                EditorGUI.DrawRect(T, OutlineColor);
                EditorGUI.DrawRect(BH, OutlineColor);
                EditorGUI.DrawRect(L, OutlineColor);
                EditorGUI.DrawRect(R, OutlineColor);

                var col = GUI.color;
                GUI.color = Color.clear;
                {
                    if (GUI.Button(H, " "))
                        foldout = !foldout;
                }
                GUI.color = col;

                EditorGUI.DrawRect(H, HeaderBoxColor);

                Rect HL = new Rect(H);
                HL.xMin = H.x + HeaderLeftPadding;

                if (foldoutHeaderTextStyle == null)
                {
                    foldoutHeaderTextStyle = new GUIStyle(EditorStyles.boldLabel);
                    foldoutHeaderTextStyle.normal.textColor = Color.white;
                }
                EditorGUI.LabelField(HL, headerText, foldoutHeaderTextStyle);

                if (foldout)
                {
                    Rect C = new Rect(H); // Content
                    C.y = BH.yMax;
                    C.height = contentHeight;

                    Rect BC = new Rect(BH); // Bottom of Content
                    BC.y += contentHeight;

                    EditorGUI.DrawRect(C, ContentBoxColor);
                    EditorGUI.DrawRect(BC, OutlineColor);
                }
            }

            private static GUIStyle enableButtonStyle;
            public static void EventFoldoutHeaderBox(ref bool foldout, string headerText, float contentHeight, GUIStyle headerTextStyle,
                in Color enabledHeaderColor, in Color enabledButtonColor, in Color enabledButtonTextColor, in Color removeButtonColor,
                ref bool enabled, out bool removePressed)
            {
                const float OutWidth = 2f;
                const float HeaderHeight = 20f;
                const float HeaderLeftPadding = 4f; // 헤더 박스 내 좌측 패딩(레이블 왼쪽 여백)
                const float ContentTopPadding = 4f; // 내용 박스 내 상단 패딩
                const float ContentBotPadding = 4f; // 내용 박스 내 하단 패딩

                contentHeight = !foldout ? 0f : (ContentTopPadding + contentHeight + ContentBotPadding);

                float totalHeight = !foldout ? (HeaderHeight) : (HeaderHeight + OutWidth + contentHeight);

                Rect H = GUILayoutUtility.GetRect(1, HeaderHeight); // Header
                GUILayoutUtility.GetRect(1f, ContentTopPadding); // Content Top Padding

                // ============================ Outlines ======================================

                Rect T = new Rect(); // Top
                T.y = H.y - OutWidth;
                T.height = OutWidth;
                T.xMin = H.xMin - OutWidth;
                T.xMax = H.xMax + OutWidth;

                Rect BH = new Rect(T); // Bottom of Header
                BH.y = H.yMax;

                Rect L = new Rect(); // Left
                L.x = H.x - OutWidth;
                L.y = H.y;
                L.width = OutWidth;
                L.height = totalHeight;

                Rect R = new Rect(L); // Right
                R.x = H.xMax;

                EditorGUI.DrawRect(T, OutlineColor);
                EditorGUI.DrawRect(BH, OutlineColor);
                EditorGUI.DrawRect(L, OutlineColor);
                EditorGUI.DrawRect(R, OutlineColor);

                // ============================ Button Rects ======================================
                Rect BTN3 = new Rect(H);
                BTN3.width = 36f;
                BTN3.x = H.xMax - BTN3.width - 4f;
                BTN3.yMin += 1f;
                BTN3.yMax -= 1f;

                Rect BTN2 = new Rect(BTN3);
                BTN2.width = 64f;
                BTN2.x = BTN3.xMin - BTN2.width - 4f;

                Rect BTN1 = new Rect(H);
                BTN1.xMax = BTN2.xMin - 4f;

                // ============================ Draw Header ======================================
                // 1. 헤더 버튼
                var col = GUI.color;
                GUI.color = Color.clear;
                {
                    if (GUI.Button(BTN1, " "))
                        foldout = !foldout;
                }
                GUI.color = col;

                // 2. 헤더 배경
                EditorGUI.DrawRect(H, enabledHeaderColor);

                // 3. 헤더 텍스트
                Rect HL = new Rect(H);
                HL.xMin = H.x + HeaderLeftPadding;

                EditorGUI.LabelField(HL, headerText, headerTextStyle);

                // 4. Enabled, Remove 버튼
                col = GUI.backgroundColor;
                GUI.backgroundColor = enabledButtonColor;
                {
                    if (enableButtonStyle == null)
                        enableButtonStyle = new GUIStyle("button");

                    enableButtonStyle.normal.textColor = enabledButtonTextColor;
                    enableButtonStyle.onNormal.textColor = enabledButtonTextColor;
                    enableButtonStyle.hover.textColor = Color.cyan * 1.5f;

                    if (GUI.Button(BTN2, enabled ? "Enabled" : "Disabled", enableButtonStyle))
                        enabled = !enabled;
                }
                GUI.backgroundColor = removeButtonColor;
                {
                    removePressed = GUI.Button(BTN3, "-");
                }
                GUI.backgroundColor = col;

                if (foldout)
                {
                    Rect C = new Rect(H); // Content
                    C.y = BH.yMax;
                    C.height = contentHeight;

                    Rect BC = new Rect(BH); // Bottom of Content
                    BC.y += contentHeight;

                    EditorGUI.DrawRect(C, ContentBoxColor);
                    EditorGUI.DrawRect(BC, OutlineColor);
                }
            }

            public class HorizontalMarginScope : GUI.Scope
            {
                private readonly float rightMargin;
                public HorizontalMarginScope(float leftMargin = 4f, float rightMargin = 4f)
                {
                    this.rightMargin = rightMargin;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(" ", GUILayout.Width(leftMargin));
                }
                protected override void CloseScope()
                {
                    EditorGUILayout.LabelField(" ", GUILayout.Width(rightMargin));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
#endif
        #endregion
        /***********************************************************************
        *                           Hierarchy Icon
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
            matNameLabelStyle.normal.textColor = goActive ? Color.magenta * 1.5f : Color.gray;

            EditorGUI.BeginDisabledGroup(!goActive);
            {
                // Priority Label
                GUI.Label(priorityLabelRect, effect.priority.ToString(), priorityLabelStyle);

                // Material Name Label
                if (effect.showMaterialNameInHierarchy && matIsNotNull)
                    GUI.Label(matNameRect, effect.effectMaterial.shader.name, matNameLabelStyle);
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
        *                           Context Menu
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
                Selection.activeGameObject = go; // 선택
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
        *                           Save Playmode Changes
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
                            //var targets = FindObjectsOfType(typeof(Inner_PlayModeSave).DeclaringType);
                            var targets = Resources.FindObjectsOfTypeAll(typeof(Inner_PlayModeSave).DeclaringType); // 비활성 오브젝트 포함
                            targetSoArr = new UnityEditor.SerializedObject[targets.Length];
                            for (int i = 0; i < targets.Length; i++)
                                targetSoArr[i] = new UnityEditor.SerializedObject(targets[i]);
                            break;

                        case UnityEditor.PlayModeStateChange.EnteredEditMode:
                            // NOTE : 플레이 도중/직후 컴파일 시 targetSoArr은 null로 초기화
                            if (targetSoArr == null) break;
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
        public static void RefClamp_00(ref this float @this)
        {
            @this *= 100f;
            @this = (int)@this * 0.01f;
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
#if !UNITY_2019_3_OR_NEWER
        public static int GetPropertyCount(this Shader shader)
        {
            return ShaderUtil.GetPropertyCount(shader);
        }
        public static Vector2 GetPropertyRangeLimits(this Shader shader, int index)
        {
            Vector2 ret = new Vector2();
            ret.x = ShaderUtil.GetRangeLimits(shader, index, 1);
            ret.y = ShaderUtil.GetRangeLimits(shader, index, 2);
            return ret;
        }
        public static string GetPropertyName(this Shader shader, int index)
        {
            return ShaderUtil.GetPropertyName(shader, index);
        }
        public static string GetPropertyDescription(this Shader shader, int index)
        {
            return ShaderUtil.GetPropertyDescription(shader, index);
        }
        public static ShaderPropertyType GetPropertyType(this Shader shader, int index)
        {
            return (ShaderPropertyType)ShaderUtil.GetPropertyType(shader, index);
        }
#endif
    }
#endif
    #endregion
}