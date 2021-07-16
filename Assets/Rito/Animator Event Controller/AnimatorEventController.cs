﻿
#if UNITY_EDITOR
#define DEBUG_ON
#endif

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
#endif

// 작성자 : Rito15
// 날짜 : 2021. 07. 15. 02:03

namespace Rito
{
    /// <summary> 애니메이터 이벤트 관리 </summary>
    [DisallowMultipleComponent]
    public class AnimatorEventController : MonoBehaviour
    {
        [System.Diagnostics.Conditional("DEBUG_ON")]
        private static void EditorLog(object msg)
        {
            Debug.Log(msg);
        }

        /***********************************************************************
        *                               Class Definition
        ***********************************************************************/
        #region .
        [Serializable]
        public class EventBundle
        {
            public EventBundle()
            {
                position = Vector3.zero;
                rotation = Vector3.zero;
                scale = Vector3.one;
                keepParentState = true;
            }

            public string name;
            public int spawnFrame;

            [HideInInspector]
            public int animationClipIndex;
            public AnimationClip animationClip;

            [Space]
            public GameObject bundlePrefab;
            public Transform spawnAxis;  // 생성 기준 트랜스폼
            public bool keepParentState = true; // 생성 이후에도 Spawn Axis의 자식으로 유지

            [Space]
            public Transform createdObject;

            // local Transforms
            public Vector3 position = Vector3.zero;
            public Vector3 rotation = Vector3.zero;
            public Vector3 scale = Vector3.one;


            [NonSerialized] public bool isPlayed = false;
#if UNITY_EDITOR
            // Created Object의 이전 트랜스폼 값들
            [NonSerialized] public Vector3 prev_createdObjPosition = Vector3.zero;
            [NonSerialized] public Vector3 prev_createdObjRotation = Vector3.zero;
            [NonSerialized] public Vector3 prev_createdObjLocalScale = Vector3.one;

            [HideInInspector] public bool edt_foldout = false;
#endif
            public bool CreatedObjIsChanged()
            {
                if (createdObject == null) return false;

                if (createdObject.position != prev_createdObjPosition) return true;
                if (createdObject.eulerAngles != prev_createdObjRotation) return true;
                if (createdObject.localScale != prev_createdObjLocalScale) return true;

                return false;
            }

            public void RecordPrevCreatedObjTransform()
            {
                prev_createdObjPosition = createdObject.position;
                prev_createdObjRotation = createdObject.eulerAngles;
                prev_createdObjLocalScale = createdObject.localScale;
            }
        }

        #endregion
        /***********************************************************************
        *                               Fields
        ***********************************************************************/
        #region .

        /// <summary> 애니메이션 재생을 멈추고 편집 모드로 진입 </summary>
        public bool _stopAndEdit;

        [Space]
        [Range(0f, 1f)]
        public float _timeScale = 1f;
        public float _timeScale_editMode = 0f;
        public int _currentFrameInt;

        [Space]
        public List<EventBundle> _bundles = new List<EventBundle>();

        private Animator _animator;
        private float _curFrame;

        /// <summary> 시간 조정 권한 가진 컴포넌트 </summary>
        private static AnimatorEventController _timeController;

        private AnimationClip[] AllAnimationClips => _animator.runtimeAnimatorController.animationClips;

        #endregion
        /***********************************************************************
        *                               Unity Events
        ***********************************************************************/
        #region .
        private void Awake()
        {
            if (!_animator) _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (AnimatorIsNotValid()) return;
            //if (EventArrayIsNotValid()) return;

#if UNITY_EDITOR
            // 시간 조정 권한 가진 경우에만 타임스케일 변경
            if (_timeController == this)
            {
                if (edt_gapCount < edt_GapCountMax)
                {
                    Time.timeScale = 1f;
                    edt_gapCount++;
                }
                else
                {
                    Time.timeScale = !_stopAndEdit ? _timeScale : _timeScale_editMode;
                }
            }

            int currentTotalFrameInt = GetCurrentTotalFrameInt();

            if (_stopAndEdit)
            {
                if (_currentFrameInt > currentTotalFrameInt)
                    _currentFrameInt %= currentTotalFrameInt;

                if (string.IsNullOrWhiteSpace(edt_forceToStartAnimatorState) == false)
                {
                    _animator.Play(edt_forceToStartAnimatorState, 0, 0);
                    edt_forceToStartAnimatorState = "";
                    _currentFrameInt = 0;
                }
                else
                {
                    string curStateName = Edt_GetCurrentState(Edt_GetAllStates()).name;
                    _animator.Play(curStateName, 0, GetCurrentNormalizedTimeFromFrame());
                }
            }
            else
#endif
            {
                _curFrame = GetCurrentAnimationFrame();
                _currentFrameInt = (int)_curFrame;
            }

            UpdateEvents();
#if UNITY_EDITOR
            UpdateOffsetsFromCreatedObject_EditorOnly();
#endif
        }

        private void OnValidate()
        {
            // 공통
            if (_bundles != null && _bundles.Count > 0)
                foreach (var bundle in _bundles)
                {
                    if (bundle.spawnFrame < 0)
                        bundle.spawnFrame = 0;
                }

#if UNITY_EDITOR
            // 에디터 모드
            if (Application.isPlaying == false)
            {
                _currentFrameInt = 0;
            }
#endif
        }
        #endregion
        /***********************************************************************
        *                               Update Methods
        ***********************************************************************/
        #region .
        /// <summary> 지정된 프레임에 각 이벤트 재생 </summary>
        private void UpdateEvents()
        {
            AnimationClip currentClip = GetCurrentAnimationClip();

            foreach (var bundle in _bundles)
            {
                // 재생 중인 클립이 설정된 클립과 다르면 무시
                if (bundle.animationClip != currentClip) continue;

                if (_currentFrameInt < bundle.spawnFrame)
                {
                    bundle.isPlayed = false;
                }

                if (!bundle.isPlayed && _currentFrameInt >= bundle.spawnFrame)
                {
                    bundle.isPlayed = true;
                    SpawnObject(bundle);
                }
            }
        }

#if UNITY_EDITOR
        /// <summary> 생성된 오브젝트의 트랜스폼을 수정하면 오프셋에 적용시키기 </summary>
        private void UpdateOffsetsFromCreatedObject_EditorOnly()
        {
            if (edt_customEditorEnabled) return;

            foreach (var bundle in _bundles)
            {
                if (bundle.createdObject == null) continue;

                //if (bundle.prev_createdLocalPosition != bundle.createdObject.localPosition)
                //    bundle.position = bundle.createdObject.localPosition;

                //if (bundle.prev_createdLocalRotation != bundle.createdObject.localEulerAngles)
                //    bundle.rotation = bundle.createdObject.localEulerAngles;

                //if (bundle.prev_createdLocalScale != bundle.createdObject.localScale)
                //    bundle.scale = bundle.createdObject.localScale;

                ModifyBundleTransformInfo(bundle);
            }
        }
#endif
        #endregion
        /***********************************************************************
        *                               Validation Methods
        ***********************************************************************/
        #region .
        private bool AnimatorIsValid()
        {
            return 
                _animator != null && 
                _animator.enabled == true &&
                AllAnimationClips.Length > 0;
        }

        private bool AnimatorIsNotValid()
        {
            return 
                _animator == null || 
                _animator.enabled == false ||
                AllAnimationClips.Length == 0;
        }

        private bool EventArrayIsValid()
        {
            return _bundles != null && _bundles.Count > 0;
        }

        private bool EventArrayIsNotValid()
        {
            return _bundles == null || _bundles.Count == 0;
        }

        #endregion
        /***********************************************************************
        *                               Getter Methods
        ***********************************************************************/
        #region .
        /// <summary> 현재 재생 중인 애니메이션 클립 정보 </summary>
        private AnimationClip GetCurrentAnimationClip()
        {
            if (AnimatorIsNotValid()) return null;

            AnimatorClipInfo[] clipInfoArr = _animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfoArr.Length == 0) return null;

            return clipInfoArr[0].clip;
        }

        /// <summary> 현재 재생 중인 애니메이션 프레임 읽어오기 </summary>
        private float GetCurrentAnimationFrame()
        {
            if (AnimatorIsNotValid()) return 0f;

            AnimatorClipInfo[] clipInfoArr = _animator.GetCurrentAnimatorClipInfo(0);
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            AnimationClip clip = clipInfoArr[0].clip;

            // 클립이 Looping이면 0~1 사이 반복, 아니면 1에서 끊기
            float normTime = clip.isLooping ? stateInfo.normalizedTime % 1f : Mathf.Clamp01(stateInfo.normalizedTime);

            float currentFrame = normTime * clip.frameRate * clip.length;

            return currentFrame;
        }

        /// <summary> 현재 프레임으로부터 정규화된 시간 구하기 </summary>
        private float GetCurrentNormalizedTimeFromFrame()
        {
            if (AnimatorIsNotValid()) return 0f;

            AnimatorClipInfo[] clipInfoArr = _animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfoArr == null || clipInfoArr.Length == 0)
                return 0f;

            AnimationClip clip = clipInfoArr[0].clip;
            float normalized = _currentFrameInt / (clip.frameRate * clip.length);

            return normalized;
        }

        /// <summary> 현재 재생 중인 클립의 전체 프레임 수 </summary>
        private int GetCurrentTotalFrameInt()
        {
            AnimationClip currentClip = GetCurrentAnimationClip();
            if (currentClip == null) return 1;

            return GetTotalFrameInt(currentClip);
        }

        /// <summary> 특정 클립의 전체 프레임 수 </summary>
        private int GetTotalFrameInt(AnimationClip clip)
        {
            if (AnimatorIsNotValid()) return 1;

            return (int)(clip.frameRate * clip.length);
        }

#if UNITY_EDITOR
        private AnimatorState[] Edt_GetAllStates()
        {
            if (AnimatorIsNotValid()) return null;

            AnimatorController controller = _animator.runtimeAnimatorController as AnimatorController;
            return controller == null ? null : controller.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state).ToArray();
        }

        private AnimatorState Edt_GetCurrentState(AnimatorState[] allStates)
        {
            if (AnimatorIsNotValid()) return null;
            if (allStates == null || allStates.Length == 0) return null;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            foreach (var state in allStates)
            {
                if (stateInfo.IsName(state.name))
                    return state;
            }

            return null;
        }
#endif

#endregion
        /***********************************************************************
        *                               Private Methods
        ***********************************************************************/
        #region .
        /// <summary> 특정 프레임에 오브젝트 생성하기 </summary>
        private void SpawnObject(EventBundle bundle)
        {
            if (bundle.bundlePrefab == null) return;

            // 기존에 생성된 오브젝트가 있었으면 지워버리기
            if (bundle.createdObject != null)
            {
                Destroy(bundle.createdObject.gameObject);
            }

#if UNITY_EDITOR
            if (_stopAndEdit)
                edt_gapCount = 0;
#endif

            Transform bundleTr = Instantiate(bundle.bundlePrefab).transform;

            if (bundle.spawnAxis)
                bundleTr.SetParent(bundle.spawnAxis);

            bundleTr.localPosition = bundle.position;
            bundleTr.localEulerAngles = bundle.rotation;
            bundleTr.localScale = bundle.scale;

#if UNITY_EDITOR
            //RememberPrevTransform(bundle);
#endif

            // 현재 생성된 오브젝트 트랜스폼 캐싱
            bundle.createdObject = bundleTr;

            if (bundle.keepParentState == false && bundle.spawnAxis != null)
            {
                bundle.createdObject.SetParent(null);
            }

            ModifyBundleTransformInfo(bundle);
        }

#if UNITY_EDITOR
        private void ModifyBundleTransformInfo(EventBundle bundle)
        {
            if (bundle.createdObject == null) return;

            // 부모 축을 기준으로 생성해서 월드 공간에 방생하는 경우 : 공간 변환 필요
            if (bundle.keepParentState == false && bundle.spawnAxis != null)
            {
                //Matrix4x4 mat = bundle.spawnAxis.worldToLocalMatrix;
                //
                //if (bundle.CreatedObjIsChanged())
                //{
                //    _currentFrameInt = bundle.spawnFrame;
                //}
                //
                //bundle.position = mat.MultiplyPoint(bundle.createdObject.position);
                //bundle.rotation = mat.MultiplyVector(bundle.createdObject.eulerAngles);
                //bundle.scale = bundle.createdObject.localScale;
            }
            else
            {
                bundle.position = bundle.createdObject.localPosition;
                bundle.rotation = bundle.createdObject.localEulerAngles;
                bundle.scale = bundle.createdObject.localScale;
            }

            bundle.RecordPrevCreatedObjTransform();
        }
#endif

        #endregion
        /***********************************************************************
        *                           Save Playmode Changes
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        static AnimatorEventController()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        private static void PlayModeStateChanged(PlayModeStateChange obj)
        {
            switch (obj)
            {
                case PlayModeStateChange.ExitingPlayMode:
                    SaveAllDataToJson();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    LoadAllDataFromJson();
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    EditorSceneManager.SaveOpenScenes();
                    break;
            }
        }

        private static string GetJsonFilePath(string fileName)
        {
            return $"{Path.Combine(Application.dataPath, $"{fileName}.txt")}";
        }

        private static void SaveAllDataToJson()
        {
            var targets = FindObjectsOfType<AnimatorEventController>();

            foreach (var target in targets)
            {
                if (target._bundles != null)
                {
                    string jsonStr = JsonUtility.ToJson(new SerializableAnimatorEventController(target));
                    File.WriteAllText(GetJsonFilePath(target.GetInstanceID().ToString()), jsonStr, System.Text.Encoding.UTF8);
                }
            }
        }

        private static void LoadAllDataFromJson()
        {
            var targets = FindObjectsOfType<AnimatorEventController>();

            foreach (var target in targets)
            {
                if (target._bundles != null)
                {
                    string filePath = GetJsonFilePath(target.GetInstanceID().ToString());

                    if (File.Exists(filePath))
                    {
                        string jsonStr = File.ReadAllText(filePath);
                        SerializableAnimatorEventController restored = JsonUtility.FromJson<SerializableAnimatorEventController>(jsonStr);

                        restored.RestoreValues(target);

                        for (int i = 0; i < target._bundles.Count; i++)
                        {
                            target._bundles[i].createdObject = null;
                        }

                        File.Delete(filePath);
                    }
                }
            }
        }

        [Serializable]
        private class SerializableAnimatorEventController
        {
            public bool stopAndEdit;
            public float timeScaleNormal;
            public float timeScaleEdit;
            public List<EventBundle> bundles;

            public SerializableAnimatorEventController(AnimatorEventController aec)
            {
                this.bundles = aec._bundles;
                this.stopAndEdit = aec._stopAndEdit;
                this.timeScaleNormal = aec._timeScale;
                this.timeScaleEdit = aec._timeScale_editMode;
            }

            public void RestoreValues(AnimatorEventController aec)
            {
                aec._bundles = this.bundles;
                aec._stopAndEdit = this.stopAndEdit;
                aec._timeScale = this.timeScaleNormal;
                aec._timeScale_editMode = this.timeScaleEdit;
            }
        }
#endif

        #endregion
        /***********************************************************************
        *                               Custom Editor
        ***********************************************************************/
        #region .
#if UNITY_EDITOR
        private int edt_bundlesArraySize = 0;
        private int edt_gapCount = edt_GapCountMax; // 오브젝트 강제 생성 시 타임스케일 잠시 복원하는 동안 카운트
        private const int edt_GapCountMax = 1;
        private string edt_forceToStartAnimatorState = ""; // 에디터에서 강제로 특정 애니메이션 상태 실행
        private bool edt_customEditorEnabled;

        [CustomEditor(typeof(AnimatorEventController))]
        private class CE : UnityEditor.Editor
        {
            private AnimatorEventController m;

            //private SerializedProperty _bundles;

            private bool isHangle = false;
            private static readonly string HanglePrefsKey = "Rito_AEC_Hangle";

            private bool bundleArrayFoldout = false;
            private static readonly string ArrayFoldoutPrefsKey = "Rito_AEC_ArrayFoldout";

            private bool animationEventFoldout = false;
            private static readonly string AnimationEventPrefsKey = "Rito_AEC_AnimationEventFoldout";

            private int totalFrameInt;

            private AnimationClip currentClip;
            private AnimationClip[] allClips;
            private string[] allClipStrings;

            private AnimatorState currentState;
            private AnimatorState[] allStates;
            private string[] allStateNames;

            private void OnEnable()
            {
                m = target as AnimatorEventController;

                if(m._animator == null)
                    m._animator = m.GetComponent<Animator>();

                //_bundles = serializedObject.FindProperty(nameof(m._bundles));

                _timeController = m; // 시간 조정 권한 양도

                isHangle = EditorPrefs.GetBool(HanglePrefsKey, false);
                bundleArrayFoldout = EditorPrefs.GetBool(ArrayFoldoutPrefsKey, false);
                animationEventFoldout = EditorPrefs.GetBool(AnimationEventPrefsKey, false);

                m.edt_customEditorEnabled = true;
            }

            private void OnDisable()
            {
                m.edt_customEditorEnabled = false;
            }

            public override void OnInspectorGUI()
            {
                if (m._animator == null)
                {
                    EditorGUILayout.HelpBox(EngHan("Animator Component Does Not Exist.", "Animator 컴포넌트가 존재하지 않습니다."),
                        MessageType.Error);
                }
                else if (m._animator.enabled == false)
                {
                    EditorGUILayout.HelpBox(EngHan("Animator Component Is Disabled.", "Animator 컴포넌트가 비활성화 상태입니다."),
                        MessageType.Warning);
                }
                else if (m.AllAnimationClips.Length == 0)
                {
                    EditorGUILayout.HelpBox(EngHan("There are NO Animation Clips in the Animator.", 
                        "Animator에 애니메이션 클립이 존재하지 않습니다."),
                        MessageType.Warning);
                }
                else
                {
                    OnInspectorGUIOnAvailable();
                }
            }

            private readonly List<EventBundle> sortedEventList = new List<EventBundle>();
            private readonly StringBuilder sortedEventInfoSb = new StringBuilder();
            private void OnInspectorGUIOnAvailable()
            {
                totalFrameInt = m.GetCurrentTotalFrameInt();
                allStates = m.Edt_GetAllStates();
                allStateNames = allStates.Select(s => s.name).ToArray();
                currentState = m.Edt_GetCurrentState(allStates);

                allClips = m.AllAnimationClips.Distinct().ToArray(); // 중복 제거 : 서로 다른 상태가 동일한 클립 가진 경우 있을 수 있음
                allClipStrings = allClips.Select(c => c.name).ToArray();
                currentClip = m.GetCurrentAnimationClip();

                Undo.RecordObject(m, "Animator Event Controller");


                DrawEngHanButton();
                DrawAnimationEventList();
                DrawAnimationControlPart();

                Space(8f);

                DrawEventsArray();

                //EditorGUILayout.PropertyField(_bundles);
                //serializedObject.ApplyModifiedProperties();
            }

            private void DrawAnimationEventList()
            {
                string eventFoldoutStr = EngHan("Registered Events", "등록된 이벤트 목록");

                bool oldFoldout = animationEventFoldout;
                animationEventFoldout = EditorGUILayout.Foldout(animationEventFoldout, eventFoldoutStr, true);

                if (oldFoldout != animationEventFoldout)
                {
                    EditorPrefs.SetBool(AnimationEventPrefsKey, animationEventFoldout);
                }
                if (animationEventFoldout)
                {
                    // 클립마다 등록된 이벤트들 나열
                    foreach (var clip in allClips)
                    {
                        sortedEventList.Clear();

                        m._bundles.ForEach(eff =>
                        {
                            if (eff.bundlePrefab != null && eff.animationClip == clip)
                                sortedEventList.Add(eff);
                        });

                        if (sortedEventList.Count == 0) continue;

                        sortedEventInfoSb.Clear();
                        sortedEventInfoSb.Append($"[{clip.name} (0 ~ {m.GetTotalFrameInt(clip)})]");

                        sortedEventList.Sort((a, b) => a.spawnFrame - b.spawnFrame);
                        sortedEventList.ForEach(eff =>
                        {
                            sortedEventInfoSb.Append($"\n - {eff.spawnFrame:D3} : ").Append(eff.name);
                        });

                        EditorGUILayout.HelpBox(sortedEventInfoSb.ToString(), MessageType.Info);
                    }

                    Space(8f);
                }
            }

            private void DrawEngHanButton()
            {
                Space(0f);
                Rect lastRect = GUILayoutUtility.GetLastRect();
                float viewWidth = lastRect.width + 23f;
                float buttonWidth = 60f;
                float buttonHeight = 20f;
                float buttonY = lastRect.yMax;
                float buttonX = viewWidth - buttonWidth - 4f;

                if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "Eng/한글"))
                {
                    isHangle = !isHangle;
                    EditorPrefs.SetBool(HanglePrefsKey, isHangle);
                }
            }

            private void DrawAnimationControlPart()
            {
                m._stopAndEdit = EditorGUILayout.Toggle(EngHan("Edit Mode(Stop)", "편집 모드(정지)"), m._stopAndEdit);

                Space(8f);

                Color oldCol = GUI.color;

                if (!m._stopAndEdit) GUI.color = Color.cyan * 1.5f;
                m._timeScale = EditorGUILayout.Slider(EngHan("Time Scale(Normal)", "게임 진행 속도(일반 모드)"), m._timeScale, 0f, 1f);

                GUI.color = oldCol;
                if (m._stopAndEdit) GUI.color = Color.cyan * 1.5f;

                m._timeScale_editMode = EditorGUILayout.Slider(EngHan("Time Scale(Edit Mode)", "게임 진행 속도(편집 모드)"), m._timeScale_editMode, 0f, 1f);

                GUI.color = oldCol;

                if (Application.isPlaying)
                {
                    Space(8f);

                    // 애니메이션 상태가 0 ~ 1개인 경우에는 굳이 애니메이션 선택 옵션 주지 않음
                    if (allStates != null && allStates.Length > 1)
                    {
                        string curClipStr = EngHan("Animator State", "애니메이션");

                        if (!m._stopAndEdit)
                        {
                            EditorGUI.BeginDisabledGroup(true);

                            EditorGUILayout.TextField(curClipStr, currentState.name);

                            EditorGUI.EndDisabledGroup();
                        }
                        else
                        {
                            int index = 0;
                            for (int i = 0; i < allStates.Length; i++)
                            {
                                if (allStates[i] == currentState)
                                {
                                    index = i;
                                    break;
                                }
                            }

                            EditorGUI.BeginChangeCheck();
                            index = EditorGUILayout.Popup(curClipStr, index, allStateNames);
                            if (EditorGUI.EndChangeCheck())
                            {
                                m.edt_forceToStartAnimatorState = allStateNames[index];

                                // 강제로 현재 재생할 애니메이터 상태 변경
                                // -> Mono의 Update 내에서 스트링 캐치하고 실행해줌
                            }
                        }
                    }

                    EditorGUI.BeginDisabledGroup(!m._stopAndEdit);
                    m._currentFrameInt = EditorGUILayout.IntSlider(EngHan("Current Frame", "현재 프레임"), m._currentFrameInt, 0, totalFrameInt);
                    EditorGUI.EndDisabledGroup();

                    if (m._stopAndEdit)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("<<")) m._currentFrameInt -= 2;
                        if (GUILayout.Button("<")) m._currentFrameInt--;
                        if (GUILayout.Button(">")) m._currentFrameInt++;
                        if (GUILayout.Button(">>")) m._currentFrameInt += 2;

                        EditorGUILayout.EndHorizontal();
                    }
                }

                Space(8f);

                if (GUILayout.Button(EngHan("Restart from Beginning", "처음부터 다시 시작")))
                {
                    RemoveAllCreatedObjects();
                    m._currentFrameInt = 0;
                    m._animator.Play(0, 0, 0);
                }
                if (GUILayout.Button(EngHan("Remove All Instantiated Events", "복제된 모든 오브젝트 제거")))
                {
                    RemoveAllCreatedObjects();
                }

                void RemoveAllCreatedObjects()
                {
                    if (m._bundles != null && m._bundles.Count > 0)
                    {
                        for (int i = 0; i < m._bundles.Count; i++)
                        {
                            if (m._bundles[i].createdObject != null)
                                Destroy(m._bundles[i].createdObject.gameObject);
                        }
                    }
                }
            }

            private void DrawEventsArray()
            {
                bool oldFoldout = bundleArrayFoldout;
                bundleArrayFoldout = EditorGUILayout.Foldout(bundleArrayFoldout, EngHan("Events", "이벤트 목록"), true);

                if (oldFoldout != bundleArrayFoldout)
                {
                    EditorPrefs.SetBool(ArrayFoldoutPrefsKey, bundleArrayFoldout);
                }

                if (bundleArrayFoldout)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    m.edt_bundlesArraySize = EditorGUILayout.IntField(EngHan("Size", "개수"), m._bundles.Count);
                    bool changed = EditorGUI.EndChangeCheck();

                    if (changed)
                    {
                        if (m.edt_bundlesArraySize < 0)
                            m.edt_bundlesArraySize = 0;

                        ref int newSize = ref m.edt_bundlesArraySize;
                        int oldSize = m._bundles.Count;

                        if (newSize < oldSize)
                        {
                            for (int i = oldSize - 1; i >= newSize; i--)
                            {
                                m._bundles.RemoveAt(i);
                            }
                        }
                        else if (newSize > oldSize)
                        {
                            for (int i = oldSize; i < newSize; i++)
                            {
                                m._bundles.Add(new EventBundle());
                            }
                        }
                    }

                    bool addNew = GUILayout.Button("+", GUILayout.Width(40f));
                    if (addNew) m._bundles.Add(new EventBundle());

                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < m._bundles.Count; i++)
                    {
                        DrawEventBundle(m._bundles[i], i);
                    }
                }


                Space(8f);

                bool addNew2 = GUILayout.Button("+");
                if (addNew2) m._bundles.Add(new EventBundle());
            }

            private void DrawEventBundle(EventBundle bundle, int index)
            {
                Space(8f);

                string name = string.IsNullOrWhiteSpace(bundle.name) ? EngHan($"Event {index}", $"이벤트 {index}") : bundle.name;

                EditorGUILayout.BeginHorizontal();

                Color oldGUIColor = GUI.color;
                GUI.color = bundle.bundlePrefab == null ? Color.red * 3f : Color.cyan;
                bundle.edt_foldout = EditorGUILayout.Foldout(bundle.edt_foldout, name, true);
                GUI.color = oldGUIColor;

                bool remove = GUILayout.Button("-", GUILayout.Width(40f));
                if (remove) m._bundles.RemoveAt(index);

                EditorGUILayout.EndHorizontal();

                if (bundle.edt_foldout)
                {
                    EditorGUI.indentLevel++;

                    // true : Spawn Axis에 위치, 회전, 크기 종속 / false : 월드 기준
                    bool isLocalState = bundle.spawnAxis != null;

                    string nameStr = EngHan("Name", "이름");
                    string animationClipStr = EngHan("Animation Clip", "애니메이션");
                    string spawnFrameStr = EngHan("Spawn Frame", "생성 프레임");
                    string prefabStr = EngHan("Prefab Object", "프리팹 오브젝트");
                    string axisStr = EngHan("Spawn Axis", "부모 트랜스폼");
                    string keepParentStr = EngHan("Keep Parent State", "부모-자식 관계 유지");
                    string createdEventStr = EngHan("Created Object", "생성된 오브젝트");
                    string posStr = isLocalState ? EngHan("Local Position", "로컬 위치") : EngHan("Global Position", "월드 위치");
                    string rotStr = isLocalState ? EngHan("Local Rotation", "로컬 회전") : EngHan("Global Rotation", "월드 회전");
                    string scaleStr = isLocalState ? EngHan("Local Scale", "로컬 크기") : EngHan("Global Scale", "월드 크기");

                    string buttonStr_jumpToSpawnFrame = EngHan("Jump to Spawn Frame", "생성 프레임으로 이동");
                    string buttonStr_createNew = EngHan("Create", "오브젝트 생성");
                    string buttonStr_remove = EngHan("Remove", "오브젝트 제거");

                    // Name
                    bundle.name = EditorGUILayout.TextField(nameStr, bundle.name);

                    // Animation Clip - Dropdown
                    if (allClips?.Length > 0)
                    {
                        // 선택 가능한 클립이 2개 이상인 경우에만 드롭다운 제공
                        if(allClips.Length >= 2)
                            bundle.animationClipIndex = EditorGUILayout.Popup(animationClipStr, bundle.animationClipIndex, allClipStrings);

                        if (bundle.animationClipIndex >= allClips.Length)
                        {
                            bundle.animationClipIndex = 0;
                        }

                        bundle.animationClip = allClips[bundle.animationClipIndex];

                        // Spawn Frame
                        bundle.spawnFrame = EditorGUILayout.IntSlider(spawnFrameStr, bundle.spawnFrame, 0, m.GetTotalFrameInt(bundle.animationClip));
                    }
                    else
                    {
                        bundle.animationClip = null;
                        bundle.animationClipIndex = 0;
                        bundle.spawnFrame = 0;
                    }


                    // Button : Jump to Spawn Frame
                    if (Application.isPlaying && m._stopAndEdit)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(" ", GUILayout.Width(28f));
                        if (GUILayout.Button(buttonStr_jumpToSpawnFrame))
                        {
                            m._currentFrameInt = bundle.spawnFrame;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    Space(8f);

                    // Event Prefab
                    EditorGUI.BeginChangeCheck();

                    Color oldGUIColor2 = GUI.color;
                    GUI.color = bundle.bundlePrefab == null ? Color.red * 2f : Color.cyan * 2f;
                    bundle.bundlePrefab = EditorGUILayout.ObjectField(prefabStr, bundle.bundlePrefab, typeof(GameObject), true) as GameObject;
                    GUI.color = oldGUIColor2;

                    if (EditorGUI.EndChangeCheck())
                    {
                        // 프리팹 등록 또는 해제 시 자동 이름 설정
                        if (bundle.bundlePrefab == null)
                            bundle.name = "";
                        else
                            bundle.name = bundle.bundlePrefab.name;
                    }

                    // Spawn Axis
                    Transform prevAxis = bundle.spawnAxis;

                    EditorGUI.BeginChangeCheck();
                    bundle.spawnAxis = EditorGUILayout.ObjectField(axisStr, bundle.spawnAxis, typeof(Transform), true) as Transform;
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (bundle.spawnAxis != null)
                        {
                            // Spawn Axis에는 프리팹 못넣게 설정
                            if (PrefabUtility.IsPartOfPrefabAsset(bundle.spawnAxis))
                            {
                                string axisWarningMsg = EngHan("Cannot register prefab assets with Spawn Axis",
                                    "프리팹 애셋은 부모 트랜스폼으로 설정될 수 없습니다.");

                                bundle.spawnAxis = prevAxis;
                                EditorUtility.DisplayDialog("Rito", axisWarningMsg, "OK");
                            }
                            else
                            {
                                // Spawn Axis에는 복제된 오브젝트 못넣게 설정
                                bool flag = false;

                                for (int i = 0; i < m._bundles.Count; i++)
                                {
                                    if (bundle.spawnAxis == m._bundles[i].createdObject)
                                    {
                                        flag = true;
                                        break;
                                    }
                                }

                                if (flag)
                                {
                                    string axisWarn2 = EngHan("Cannot register instantiated object with Spawn Axis",
                                        "복제된 오브젝트를 부모 트랜스폼으로 설정할 수 없습니다.");

                                    bundle.spawnAxis = prevAxis;
                                    EditorUtility.DisplayDialog("Rito", axisWarn2, "OK");
                                }
                            }
                        }

                        // Axis가 null이었다가 새로 등록되면 keepState 기본 값은 true
                        if (prevAxis == null && bundle.spawnAxis != null)
                            bundle.keepParentState = true;

                        ApplyPosRotSclChangesToCreatedObject();
                    }

                    // Keep Parent State
                    if (bundle.spawnAxis == null)
                    {
                        bundle.keepParentState = false;
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        bundle.keepParentState = EditorGUILayout.Toggle(keepParentStr, bundle.keepParentState);
                        if (EditorGUI.EndChangeCheck())
                        {
                            ApplyPosRotSclChangesToCreatedObject();
                        }
                    }

                    void ApplyPosRotSclChangesToCreatedObject()
                    {
                        if (bundle.createdObject != null)
                        {
                            // Spawn Axis가 존재하는 경우
                            if (bundle.spawnAxis != null)
                            {
                                // 월드에 방생
                                if (bundle.keepParentState == false)
                                {
                                    bundle.position = bundle.createdObject.localPosition;
                                    bundle.rotation = bundle.createdObject.localEulerAngles;
                                    bundle.scale = bundle.createdObject.localScale;

                                    bundle.createdObject.SetParent(null);
                                }
                                // 로컬에 유지
                                else
                                {
                                    bundle.createdObject.SetParent(bundle.spawnAxis);

                                    bundle.position = bundle.createdObject.localPosition;
                                    bundle.rotation = bundle.createdObject.localEulerAngles;
                                    bundle.scale = bundle.createdObject.localScale;
                                }
                            }
                            // Spawn Axis가 존재하지 않는 경우 - 기준을 월드로 설정
                            else
                            {
                                bundle.createdObject.SetParent(null);

                                bundle.position = bundle.createdObject.position;
                                bundle.rotation = bundle.createdObject.eulerAngles;
                                bundle.scale = bundle.createdObject.lossyScale;
                            }

                            EditorLog("Here");
                        }
                    }

                    // Created Object
                    if (Application.isPlaying)
                    {
                        Color oldCol = GUI.color;
                        if (bundle.createdObject != null)
                            GUI.color = Color.cyan * 1.5f;

                        EditorGUI.BeginDisabledGroup(true);
                        _ = EditorGUILayout.ObjectField(createdEventStr, bundle.createdObject, typeof(GameObject), true) as GameObject;
                        EditorGUI.EndDisabledGroup();

                        GUI.color = oldCol;
                    }

                    Space(2f);

                    // 월드에 방생하는 경우, 동일 프레임 아니면 수정 불가
                    bool axisExistsAndKeepParent =
                    !(
                        bundle.spawnAxis == null ||
                        (bundle.keepParentState == true) ||
                        (m._currentFrameInt == bundle.spawnFrame && currentClip == bundle.animationClip)
                    );

                    // Buttons : Create, Remove
                    if (Application.isPlaying)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(" ", GUILayout.Width(28f));

                        EditorGUI.BeginDisabledGroup(axisExistsAndKeepParent);

                        if (GUILayout.Button(buttonStr_createNew))
                        {
                            m.SpawnObject(bundle);
                        }

                        EditorGUI.EndDisabledGroup();

                        if (GUILayout.Button(buttonStr_remove) && bundle.createdObject != null)
                        {
                            if(bundle.createdObject != null)
                                Destroy(bundle.createdObject.gameObject);
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    Space(8f);

                    EditorGUI.BeginDisabledGroup(axisExistsAndKeepParent);

                    // Pos, Rot, Scale
                    EditorGUI.BeginChangeCheck();

                    bundle.position = EditorGUILayout.Vector3Field(posStr, bundle.position);
                    bundle.rotation = EditorGUILayout.Vector3Field(rotStr, bundle.rotation);
                    bundle.scale    = EditorGUILayout.Vector3Field(scaleStr, bundle.scale);

                    if (EditorGUI.EndChangeCheck() && bundle.createdObject != null)
                    {
                        if (bundle.spawnAxis != null)
                        {
                            if (bundle.keepParentState)
                            {
                                bundle.createdObject.localPosition = bundle.position;
                                bundle.createdObject.localEulerAngles = bundle.rotation;
                                bundle.createdObject.localScale = bundle.scale;
                            }
                            else
                            {
                                Matrix4x4 mat = bundle.spawnAxis.localToWorldMatrix;

                                bundle.createdObject.position = mat.MultiplyPoint(bundle.position);
                                bundle.createdObject.eulerAngles = mat.MultiplyVector(bundle.rotation);
                                bundle.createdObject.localScale = mat.MultiplyVector(bundle.scale);
                            }
                        }
                        else
                        {
                            bundle.createdObject.localPosition = bundle.position;
                            bundle.createdObject.localEulerAngles = bundle.rotation;
                            bundle.createdObject.localScale = bundle.scale;
                        }
                    }

                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel--;
                }
            }

            private string EngHan(string eng, string han)
            {
                return !isHangle ? eng : han;
            }

            private void Space(float width)
            {
#if UNITY_2019_1_OR_NEWER
                EditorGUILayout.Space(width);
#else
                EditorGUILayout.Space();
#endif
            }

        } // Custom Editor Class End

        [MenuItem("CONTEXT/Animator/Add Animator Event Controller")]
        private static void Context_AddComponent(MenuCommand command)
        {
            var anim = command.context as Animator;
            anim.gameObject.AddComponent<AnimatorEventController>();
        }

        // 활성화 / 비활성화 여부 결정
        [MenuItem("CONTEXT/Animator/Add Animator Event Controller", true)]
        private static bool Context_AddComponentValidate(MenuCommand command)
        {
            var anim = command.context as Animator;
            return anim.GetComponent<AnimatorEventController>() == null;
        }
#endif
        #endregion
    }
}