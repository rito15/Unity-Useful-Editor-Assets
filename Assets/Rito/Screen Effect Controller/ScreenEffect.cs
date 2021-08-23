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
 *          T            O                 D               O
 * 
 *  - 에디터 버전 2018, 2019.1, 2020 호환 테스트
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

        [SerializeField] private bool showMaterialNameInHierarchy = true;
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

        [SerializeField] private bool __optionFoldout1 = true;
        [SerializeField] private bool __optionFoldout2 = true;
        [SerializeField] private bool __optionFoldout3 = true;
        [SerializeField] private bool __matPropListFoldout = true;

        [SerializeField] private int __propCount; // 마테리얼 프로퍼티 개수 기억(변화 감지용)

        /// <summary> 마테리얼의 초깃값 기억 </summary>
        [SerializeField]
        private MaterialPropertyValue[] __materialDefaultValues;

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
            if (durationSeconds <= 0f)
                return;

            for(int i = 0; i < matPropertyList.Count; i++)
            {
                var mp = matPropertyList[i];
                if (mp == null || mp.eventList == null || mp.eventList.Count == 0)
                    continue;
#if UNITY_EDITOR
                if (mp.enabled == false)
                    continue;
#endif

                var eventList = mp.eventList;
                int eventCount = eventList.Count - 1;

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
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                // 해당하는 시간 구간이 아닐 경우, 판정하지 않음
                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);

                                // REMAP
                                float curValue = Mathf.Lerp(prevEvent.floatValue, nextEvent.floatValue, t);

                                effectMaterial.SetFloat(mp.propName, curValue);
                            }
                            break;

                        case ShaderPropertyType.Color:
                            for (int j = 0; j < eventCount; j++)
                            {
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);
                                Color curValue = Color.Lerp(prevEvent.color, nextEvent.color, t);

                                effectMaterial.SetColor(mp.propName, curValue);
                            }
                            break;

                        case ShaderPropertyType.Vector:
                            for (int j = 0; j < eventCount; j++)
                            {
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentSeconds < prevEvent.time || nextEvent.time <= currentSeconds) continue;
                                float t = (currentSeconds - prevEvent.time) / (nextEvent.time - prevEvent.time);
                                Vector4 curValue = Vector4.Lerp(prevEvent.vector4, nextEvent.vector4, t);

                                effectMaterial.SetVector(mp.propName, curValue);
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
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                float curValue = Mathf.Lerp(prevEvent.floatValue, nextEvent.floatValue, t);

                                effectMaterial.SetFloat(mp.propName, curValue);
                            }
                            break;

                        case ShaderPropertyType.Color:
                            for (int j = 0; j < eventCount; j++)
                            {
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                Color curValue = Color.Lerp(prevEvent.color, nextEvent.color, t);

                                effectMaterial.SetColor(mp.propName, curValue);
                            }
                            break;

                        case ShaderPropertyType.Vector:
                            for (int j = 0; j < eventCount; j++)
                            {
                                var prevEvent = eventList[j];
                                var nextEvent = eventList[j + 1];

                                if (currentFrame < prevEvent.frame || nextEvent.frame <= currentFrame) continue;
                                float t = (float)(currentFrame - prevEvent.frame) / (nextEvent.frame - prevEvent.frame);
                                Vector4 curValue = Vector4.Lerp(prevEvent.vector4, nextEvent.vector4, t);

                                effectMaterial.SetVector(mp.propName, curValue);
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
            public string displayName;
            public ShaderPropertyType propType;
            public int propIndex;

            public bool enabled;

#if UNITY_EDITOR
            public bool __foldout = true;
            public int __playingIndex = 0; // 현재 재생 중인 이벤트의 인덱스
#endif

            public List<MaterialPropertyValue> eventList;

            public bool HasEvents => eventList != null && eventList.Count > 0;

            public MaterialPropertyInfo(Material material, string name, string displayName, ShaderPropertyType type, int propIndex)
            {
                this.material = material;
                this.propName = name;
                this.displayName = displayName;
                this.propType = type;
                this.propIndex = propIndex;
                this.enabled = false;

                this.eventList = new List<MaterialPropertyValue>(10);
            }

            /// <summary> 이벤트가 아예 없었던 경우, 초기 이벤트 2개(시작, 끝) 추가 </summary>
            public void AddInitialEvents(float duration, bool isTimeModeSeconds)
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
                        begin.range = end.range = material.shader.GetPropertyRangeLimits(propIndex);
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
            public void AddNewEvent(int index, bool isTimeModeSeconds)
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
                SetPropertyValueFromMaterial(newValue);

                eventList.Insert(index + 1, newValue);
            }

            private void SetPropertyValueFromMaterial(MaterialPropertyValue dest)
            {
                switch (propType)
                {
                    case ShaderPropertyType.Float:
                        dest.floatValue = material.GetFloat(propName);
                        break;

                    case ShaderPropertyType.Range:
                        dest.floatValue = material.GetFloat(propName);
                        dest.range = material.shader.GetPropertyRangeLimits(propIndex);
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
            public void RemoveAllEvents()
            {
                this.eventList.Clear();
                this.enabled = false;
            }
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

            /// <summary> 시간을 제외한 모든 값 복제하여 전달 </summary>
            public void CopyValuesWithoutTime(MaterialPropertyValue other)
            {
                other.vector4 = this.vector4;
            }
        }

        [SerializeField]
        private List<MaterialPropertyInfo> matPropertyList = new List<MaterialPropertyInfo>(20);

        #endregion
        /***********************************************************************
        *                               Custom Editor
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        [CustomEditor(typeof(ScreenEffect))]
        private class CE : UnityEditor.Editor
        {
            private ScreenEffect m;

            private Material material;
            private Shader shader;

            private bool isMaterialChanged; // 제거 예정

            private static readonly Color MinusButtonColor = Color.red * 1.5f;
            private static readonly Color TimeColor = new Color(1.5f, 1.5f, 0.2f, 1f);  // Yellow
            private static readonly Color EnabledColor = new Color(0f, 1.5f, 1.5f, 1f); // Cyan

            private static GUIStyle bigMinusButtonStyle;
            private static GUIStyle boldFoldoutStyle;
            private static GUIStyle propertyEventTimeLabelStyle;

            private void OnEnable()
            {
                m = target as ScreenEffect;

                m.__OnEditorUpdate -= Repaint;
                m.__OnEditorUpdate += Repaint;

                isHangle = EditorPrefs.GetBool(EngHanPrefKey, false);
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
                        //Debug.Log("NULL Material");
                    }
                    else
                    {
                        // 마테리얼 정보가 변한 경우, 전체 마테리얼 프로퍼티 및 이벤트 목록 초기화
                        if (isMaterialChanged)
                        {
                            LoadMaterialInfo(); 
                        }

                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                            DrawEditorOptions();
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        try
                        {
                            DrawCopiedMaterialProperties();
                        }
                        catch // 쉐이더 프로퍼티 변경 시 정보 다시 로드
                        {
                            if (m.effectMaterial != null)
                                LoadMaterialInfo();
                            else
                                m.effectMaterial = null;
                        }

                        try
                        {
                            EditorGUILayout.Space();
                        }
                        catch { } // 쉐이더 프로퍼티 리로드 시 발생하는 예외 무시

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
            private void LoadMaterialInfo()
            {
                InitVariables();
                InitMaterialProperties();
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
            }
            private void InitMaterialProperties()
            {
                // 기존 이벤트들 백업
                var backup = m.matPropertyList;

                int propertyCount = shader.GetPropertyCount();
                m.matPropertyList = new List<MaterialPropertyInfo>(propertyCount);

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

                // 동일 쉐이더일 경우, 백업된 이벤트들에서 동일하게 존재하는 프로퍼티에 이벤트 복제
                if (m.matPropertyList.Count > 0 && m.matPropertyList[0].material.shader == shader)
                {
                    for (int i = 0; i < m.matPropertyList.Count; i++)
                    {
                        MaterialPropertyInfo cur = m.matPropertyList[i];
                        MaterialPropertyInfo found = backup.Find(x => 
                            x.HasEvents &&
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

                // 마테리얼 기본 값들 기억
                m.__materialDefaultValues = new MaterialPropertyValue[m.matPropertyList.Count];
                for (int i = 0; i < m.__materialDefaultValues.Length; i++)
                {
                    var currentInfo = m.matPropertyList[i];
                    var currentValue = m.__materialDefaultValues[i] = new MaterialPropertyValue();

                    switch (currentInfo.propType)
                    {
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            currentValue.floatValue = material.GetFloat(currentInfo.propName);
                            break;
                        case ShaderPropertyType.Vector:
                            currentValue.vector4 = material.GetVector(currentInfo.propName);
                            break;
                        case ShaderPropertyType.Color:
                            currentValue.color = material.GetColor(currentInfo.propName);
                            break;
                    }
                }
                
            }
            #endregion
            /************************************************************************
             *                               Drawing Methods
             ************************************************************************/
            #region .
            private static readonly string[] StopActionsHangle = new string[]
            {
                "파괴", "비활성화", "반복(재시작)"
            };
            private static readonly string[] TimeModesEng = new string[]
            {
                "Time(Second)", "Frame"
            };
            private static readonly string[] TimeModesHan = new string[]
            {
                "시간(초)", "프레임"
            };

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
                        if(m.effectMaterial != null)
                            m.effectMaterial = new Material(m.effectMaterial);
                    }

                    // 마테리얼 재할당
                    if (RitoEditorGUI.DrawButtonLayout("Reload", Color.white, Color.black, 60f))
                    {
                        if (m.effectMaterial != null)
                        {
                            //m.effectMaterial = new Material(m.effectMaterial);
                            LoadMaterialInfo();
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
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].HasEvents == false)
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
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].HasEvents == false)
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

                using (new RitoEditorGUI.HorizontalMarginScope())
                {
                    RitoEditorGUI.DrawPrefixLabelLayout(m.isTimeModeSeconds ? EngHan("Duration Time", "지속 시간(초)") : EngHan("Duration Frame", "지속 시간(프레임)"));

                    // 1. 시간 계산 방식이 초 일경우
                    if (m.isTimeModeSeconds)
                    {
                        EditorGUI.BeginChangeCheck();

                        m.durationSeconds.RefClamp_00();
                        float prevDuration = m.durationSeconds;

                        m.durationSeconds = EditorGUILayout.FloatField(m.durationSeconds);
                        if (m.durationSeconds < 0f) m.durationSeconds = 0f;

                        // Duration 변경 시, 비율을 유지하면서 이벤트들의 Time 변경
                        if (EditorGUI.EndChangeCheck() && m.durationSeconds > 0f)
                        {
                            float changeRatio = m.durationSeconds / prevDuration;

                            for (int i = 0; i < m.matPropertyList.Count; i++)
                            {
                                if (m.matPropertyList[i] == null || m.matPropertyList[i].HasEvents == false)
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

                        m.durationFrame = EditorGUILayout.IntField(m.durationFrame);
                        if (m.durationFrame < 0) m.durationFrame = 0;

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
                    if(m.isTimeModeSeconds)
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

                EditorGUI.BeginDisabledGroup(Application.isPlaying && !m.__editMode);


                EditorGUILayout.BeginHorizontal();

                // LEFT
                EditorGUILayout.BeginVertical();
                {
                    for (int i = 0; i < m.matPropertyList.Count; i++)
                    {
                        var mp = m.matPropertyList[i];
                        if (mp.propType == ShaderPropertyType.Texture)
                            continue;

                        Color currentColor = mp.enabled ? EnabledColor : Color.gray;
                        bool hasEvents = mp.HasEvents;

                        EditorGUILayout.BeginHorizontal(); // ================= Horizontal Begin ===============

                        RitoEditorGUI.DrawHorizontalSpace(4f);

                        // Draw Label
                        RitoEditorGUI.DrawPrefixLabelLayout(mp.displayName,
                            hasEvents ? currentColor : Color.white, 0.25f);

                        Color guiColor = GUI.color;
                        if (hasEvents)
                            GUI.color = currentColor;
                        {
                            // Draw Property
                            switch (mp.propType)
                            {
                                case ShaderPropertyType.Float:
                                    {
                                        float value = EditorGUILayout.FloatField(material.GetFloat(mp.propName));
                                        material.SetFloat(mp.propName, value);
                                    }
                                    break;
                                case ShaderPropertyType.Range:
                                    {
                                        Vector2 minMax = shader.GetPropertyRangeLimits(mp.propIndex);
                                        float value = EditorGUILayout.Slider(material.GetFloat(mp.propName), minMax.x, minMax.y);
                                        material.SetFloat(mp.propName, value);
                                    }
                                    break;
                                case ShaderPropertyType.Vector:
                                    {
                                        Vector4 value = EditorGUILayout.Vector4Field("", material.GetVector(mp.propName));
                                        material.SetVector(mp.propName, value);
                                    }
                                    break;
                                case ShaderPropertyType.Color:
                                    {
                                        Color value = EditorGUILayout.ColorField(material.GetColor(mp.propName));
                                        material.SetColor(mp.propName, value);
                                    }
                                    break;
                            }
                        }
                        GUI.color = guiColor;

                        RitoEditorGUI.DrawHorizontalSpace(4f);

                        EditorGUILayout.EndHorizontal(); // ================= Horizontal End ===============
                    }
                }
                EditorGUILayout.EndVertical();

                // RIGHT
                EditorGUILayout.BeginVertical();
                {
                    for (int i = 0; i < m.matPropertyList.Count; i++)
                    {
                        var mp = m.matPropertyList[i];
                        if (mp.propType == ShaderPropertyType.Texture)
                            continue;

                        Color currentColor = mp.enabled ? EnabledColor : Color.gray;
                        bool hasEvents = mp.HasEvents;

                        EditorGUILayout.BeginHorizontal();

                        // 각 프로퍼티마다 리셋 버튼 - 클릭 시 백업된 기본값으로 마테리얼 프로퍼티 값 변경
                        if (RitoEditorGUI.DrawButtonLayout("R", Color.magenta, 20f, 18f))
                        {
                            switch (mp.propType)
                            {
                                case ShaderPropertyType.Float:
                                case ShaderPropertyType.Range:
                                    material.SetFloat(mp.propName, m.__materialDefaultValues[i].floatValue);
                                    break;
                                case ShaderPropertyType.Vector:
                                    material.SetVector(mp.propName, m.__materialDefaultValues[i].vector4);
                                    break;
                                case ShaderPropertyType.Color:
                                    material.SetColor(mp.propName, m.__materialDefaultValues[i].color);
                                    break;
                            }
                        }

                        RitoEditorGUI.DrawHorizontalSpace(4f);

                        // 프로퍼티에 이벤트 존재할 경우 : 활성화, 제거 버튼
                        if (hasEvents)
                        {
                            string enableButtonString = mp.enabled ? "E" : "D";

                            if (RitoEditorGUI.DrawButtonLayout(enableButtonString, currentColor, 20f, 18f))
                                mp.enabled = !mp.enabled;

                            if (RitoEditorGUI.DrawButtonLayout("-", Color.red * 1.5f, 20f, 18f))
                                mp.RemoveAllEvents();
                        }
                        // 이벤트 없을 경우 : 추가 버튼
                        else
                        {
                            bool addButton = RitoEditorGUI.DrawButtonLayout("+", Color.green * 1.5f, 42f, 18f);
                            if (addButton)
                            {
                                mp.AddInitialEvents(m.isTimeModeSeconds ? m.durationSeconds : m.durationFrame, m.isTimeModeSeconds);
                            }
                        }

                        RitoEditorGUI.DrawHorizontalSpace(4f);

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                EditorGUI.EndDisabledGroup();
            }
            private void DrawCopiedMaterialProperties___BACKUP_____()
            {
                RitoEditorGUI.FoldoutHeaderBox(ref m.__matPropListFoldout, EngHan("Material Properties", "마테리얼 프로퍼티 목록"),
                    m.matPropertyList.Count);

                if (!m.__matPropListFoldout)
                    return;

                EditorGUI.BeginDisabledGroup(Application.isPlaying && !m.__editMode);

                for (int i = 0; i < m.matPropertyList.Count; i++)
                {
                    var mp = m.matPropertyList[i];
                    if (mp.propType == ShaderPropertyType.Texture)
                        continue;

                    Color currentColor = mp.enabled ? EnabledColor : Color.gray;
                    bool hasEvents = mp.HasEvents;

                    EditorGUILayout.BeginHorizontal();

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    RitoEditorGUI.DrawPrefixLabelLayout(mp.displayName,
                        hasEvents ? currentColor : Color.white);

                    Color guiColor = GUI.color;
                    if (hasEvents)
                        GUI.color = currentColor;

                    switch (mp.propType)
                    {
                        case ShaderPropertyType.Float:
                            {
                                float value = EditorGUILayout.FloatField(material.GetFloat(mp.propName));
                                material.SetFloat(mp.propName, value);
                            }
                            break;
                        case ShaderPropertyType.Range:
                            {
                                Vector2 minMax = shader.GetPropertyRangeLimits(mp.propIndex);
                                float value = EditorGUILayout.Slider(material.GetFloat(mp.propName), minMax.x, minMax.y);
                                material.SetFloat(mp.propName, value);
                            }
                            break;
                        case ShaderPropertyType.Vector:
                            {
                                Vector4 value = EditorGUILayout.Vector4Field("", material.GetVector(mp.propName));
                                material.SetVector(mp.propName, value);
                            }
                            break;
                        case ShaderPropertyType.Color:
                            {
                                Color value = EditorGUILayout.ColorField(material.GetColor(mp.propName));
                                material.SetColor(mp.propName, value);
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
                                material.SetFloat(mp.propName, m.__materialDefaultValues[i].floatValue);
                                break;
                            case ShaderPropertyType.Vector:
                                material.SetVector(mp.propName, m.__materialDefaultValues[i].vector4);
                                break;
                            case ShaderPropertyType.Color:
                                material.SetColor(mp.propName, m.__materialDefaultValues[i].color);
                                break;
                        }
                    }

                    RitoEditorGUI.DrawHorizontalSpace(4f);

                    // 프로퍼티에 이벤트 존재할 경우 : 활성화, 제거 버튼
                    if (hasEvents)
                    {
                        string enableButtonString = mp.enabled ? "E" : "D";

                        if (RitoEditorGUI.DrawButtonLayout(enableButtonString, currentColor, 20f, 18f))
                            mp.enabled = !mp.enabled;

                        if (RitoEditorGUI.DrawButtonLayout("-", Color.red * 1.5f, 20f, 18f))
                            mp.RemoveAllEvents();
                    }
                    // 이벤트 없을 경우 : 추가 버튼
                    else
                    {
                        bool addButton = RitoEditorGUI.DrawButtonLayout("+", Color.green * 1.5f, 42f, 18f);
                        if (addButton)
                        {
                            mp.AddInitialEvents(m.isTimeModeSeconds ? m.durationSeconds : m.durationFrame, m.isTimeModeSeconds);
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
                for (int i = 0; i < m.matPropertyList.Count; i++)
                {
                    if (m.matPropertyList[i].HasEvents)
                    {
                        DrawPropertyEvents(m.matPropertyList[i], () =>
                            {
                                // [-] 버튼 클릭하면 해당 프로퍼티에서 이벤트들 싹 제거
                                m.matPropertyList[i].RemoveAllEvents();
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
            /// <summary> 프로퍼티 하나의 이벤트 모두 그리기 </summary>
            private void DrawPropertyEvents(in MaterialPropertyInfo matProp, Action removeAction)
            {
                ref bool enabled = ref matProp.enabled;

                int eventCount = matProp.eventList.Count;
                int contentCount = eventCount * 3 - 1;
                if (matProp.propType == ShaderPropertyType.Color)
                    contentCount += eventCount;

                float heightAdj = matProp.propType == ShaderPropertyType.Color ? eventCount : 0f;

                if (eventFoldoutHeaderStyle == null)
                    eventFoldoutHeaderStyle = new GUIStyle(EditorStyles.label);
                eventFoldoutHeaderStyle.normal.textColor =
                eventFoldoutHeaderStyle.onNormal.textColor = enabled ? Color.black : Color.gray;
                eventFoldoutHeaderStyle.fontStyle = FontStyle.Bold;

                string headerText = $"{matProp.displayName} [{matProp.propType}]";
                Color headerBgColor = enabled ? new Color(0f, 0.8f, 0.8f) : new Color(0.1f, 0.1f, 0.1f);

                Color enableButtonTextColor = enabled ? Color.white : Color.white;
                Color enableButtonBgColor = enabled ? Color.black : Color.gray;
                Color removeButtonColor = Color.red * 1.8f;

                RitoEditorGUI.EventFoldoutHeaderBox(ref matProp.__foldout, headerText, contentCount, heightAdj, eventFoldoutHeaderStyle,
                    headerBgColor, enableButtonBgColor, enableButtonTextColor, removeButtonColor,
                    ref matProp.enabled, out bool removePressed);

                if (removePressed)
                {
                    removeAction();
                }

                if (matProp.__foldout)
                {
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
                            RitoEditorGUI.DrawHorizontalSpace((EditorGUIUtility.currentViewWidth) * 0.5f - ButtonWidth);
                            if (RitoEditorGUI.DrawPlusButtonLayout(ButtonWidth)) addNewEvent = i;

                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    if (addNewEvent > -1)
                    {
                        matProp.AddNewEvent(addNewEvent, m.isTimeModeSeconds);
                    }
                }
            }

            private static readonly Color HighlightBasic = new Color(0.28f, 0.28f, 0.28f);
            private static readonly Color HighlightPlaying = new Color(0f, 0.6f, 0.8f);

            /// <summary> 프로퍼티의 이벤트 하나 그리기 (시간, 값) </summary>
            private void DrawEachEvent(MaterialPropertyInfo mp, MaterialPropertyValue mpEvent, int index)
            {
                bool isFirst = index == 0;
                bool isLast = index == mp.eventList.Count - 1;
                bool isFirstOrLast = isFirst || isLast;

                // Clamp Time Value (First, Last)
                if (isFirst) mpEvent.time = 0f;
                else if (isLast) mpEvent.time = m.durationSeconds;


                // 현재 재생, 보간되는 두 이벤트 배경 하이라이트
                bool currentPlaying = 
                    Application.isPlaying && 
                    m.isActiveAndEnabled && 
                    (index == mp.__playingIndex || index - 1 == mp.__playingIndex);

                // 추가된 이벤트마다 배경 하이라이트
                if (currentPlaying || isFirstOrLast == false)
                {
                    Rect highlightRect = GUILayoutUtility.GetRect(1f, 0f);
                    highlightRect.height = mp.propType == ShaderPropertyType.Color ? 62f : 42f;
                    highlightRect.xMin += 4f;
                    highlightRect.xMax -= 4f;
                    EditorGUI.DrawRect(highlightRect, currentPlaying ? HighlightPlaying : HighlightBasic);
                }


                const float MinusButtonWidth = 40f;
                const float LabelWidth = 80f;
                const float RightButtonMargin = 6f;

                // 1. Time 슬라이더
                if (isFirstOrLast) EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.BeginHorizontal();
                {
                    RitoEditorGUI.DrawHorizontalSpace(4f);

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
                    if(isFirstOrLast == false)
                        GUI.color = TimeColor;

                    // [1] 시간 계산 방식 : 초
                    if (m.isTimeModeSeconds)
                    {
                        mpEvent.time.RefClamp_00();
                        mpEvent.time = EditorGUILayout.Slider(mpEvent.time, 0f, m.durationSeconds);
                    }
                    // [2] 시간 계산 방식 : 프레임
                    else
                    {
                        mpEvent.frame = EditorGUILayout.IntSlider(mpEvent.frame, 0, m.durationFrame);
                    }

                    GUI.color = guiColor;

                    // 값 변경 시, 전후값의 경계에서 전후값 변경
                    if(isFirstOrLast == false)
                    {
                        MaterialPropertyValue prevEvent = mp.eventList[index - 1];
                        MaterialPropertyValue nextEvent = mp.eventList[index + 1];

                        if (m.isTimeModeSeconds)
                        {
                            if (prevEvent.time > mpEvent.time)
                                prevEvent.time = mpEvent.time;
                            if (nextEvent.time < mpEvent.time)
                                nextEvent.time = mpEvent.time;
                        }
                        else
                        {
                            if (prevEvent.frame > mpEvent.frame)
                                prevEvent.frame = mpEvent.frame;
                            if (nextEvent.frame < mpEvent.frame)
                                nextEvent.frame = mpEvent.frame;
                        }
                    }

                    // 여백 생성
                    RitoEditorGUI.DrawHorizontalSpace(MinusButtonWidth);
                    Rect minusButtonRect = GUILayoutUtility.GetLastRect();
                    minusButtonRect.height *= mp.propType == ShaderPropertyType.Color ? 2f : 1f;
                    minusButtonRect.height += mp.propType == ShaderPropertyType.Color ? 4f : 1f;
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

                RitoEditorGUI.DrawHorizontalSpace(4f);
                RitoEditorGUI.DrawPrefixLabelLayout(EngHan("Value", "값"), Color.white, LabelWidth, true);

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
        *                               Custom EditorGUI
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

                if(!fixedWidth)
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
                float totalHeight   = !foldout ? (HeaderHeight) : (HeaderHeight + OutWidth + contentHeight);

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
            public static void EventFoldoutHeaderBox(ref bool foldout, string headerText, int contentCount, float heightAdjust, GUIStyle headerTextStyle,
                in Color enabledHeaderColor, in Color enabledButtonColor, in Color enabledButtonTextColor, in Color removeButtonColor, 
                ref bool enabled, out bool removePressed)
            {
                const float OutWidth = 2f;
                const float HeaderHeight = 20f;
                const float OneHeight = 21f;
                const float HeaderLeftPadding = 4f; // 헤더 박스 내 좌측 패딩(레이블 왼쪽 여백)
                const float ContentTopPadding = 4f; // 내용 박스 내 상단 패딩
                const float ContentBotPadding = 4f; // 내용 박스 내 하단 패딩
                float contentHeight = !foldout ? 0f : (ContentTopPadding + OneHeight * contentCount + ContentBotPadding);
                contentHeight += heightAdjust;

                float totalHeight   = !foldout ? (HeaderHeight) : (HeaderHeight + OutWidth + contentHeight);

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
                BTN2.width = 60f;
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
    }
#endif
    #endregion
}