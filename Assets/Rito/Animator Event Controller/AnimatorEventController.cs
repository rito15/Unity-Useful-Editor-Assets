﻿using System;
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
    /// <summary> 애니메이터가 현재 재생 중인 애니메이션의 특정 프레임에 생성되는 이펙트 관리 </summary>
    [DisallowMultipleComponent]
    public class AnimatorEventController : MonoBehaviour
    {
        /***********************************************************************
        *                               Class Definition
        ***********************************************************************/
        #region .
        [Serializable]
        public class EffectBundle
        {
            public EffectBundle()
            {
                position = Vector3.zero;
                rotation = Vector3.zero;
                scale = Vector3.one;
            }

            public string name;
            public int spawnFrame;

            [HideInInspector]
            public int animationClipIndex;
            public AnimationClip animationClip;

            [Space]
            public GameObject effectPrefab;
            public Transform spawnAxis;

            [Space]
            public Transform createdEffect;

            // local Transforms
            public Vector3 position = Vector3.zero;
            public Vector3 rotation = Vector3.zero;
            public Vector3 scale = Vector3.one;


            [NonSerialized] public bool isPlayed = false;
#if UNITY_EDITOR
            // Created Effect의 이전 트랜스폼 값들
            [NonSerialized] public Vector3 prev_createdLocalPosition = Vector3.zero;
            [NonSerialized] public Vector3 prev_createdLocalRotation = Vector3.zero;
            [NonSerialized] public Vector3 prev_createdLocalScale = Vector3.one;

            [HideInInspector] public bool edt_foldout = false;
#endif
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
        public List<EffectBundle> _effects;

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
            if (EffectArrayIsNotValid()) return;

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

            UpdateEffects();
#if UNITY_EDITOR
            UpdateOffsetsFromCreatedEffect_EditorOnly();
#endif
        }

        private void OnValidate()
        {
            // 공통
            if (_effects != null && _effects.Count > 0)
                foreach (var effect in _effects)
                {
                    if (effect.spawnFrame < 0)
                        effect.spawnFrame = 0;
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
        /// <summary> 지정된 프레임에 각 이펙트 재생 </summary>
        private void UpdateEffects()
        {
            AnimationClip currentClip = GetCurrentAnimationClip();

            foreach (var effect in _effects)
            {
                // 재생 중인 클립이 설정된 클립과 다르면 무시
                if (effect.animationClip != currentClip) continue;

                if (_currentFrameInt < effect.spawnFrame)
                {
                    effect.isPlayed = false;
                }

                if (!effect.isPlayed && _currentFrameInt >= effect.spawnFrame)
                {
                    effect.isPlayed = true;
                    SpawnEffect(effect);
                }
            }
        }

#if UNITY_EDITOR
        /// <summary> 생성된 이펙트 트랜스폼을 수정하면 오프셋에 적용시키기 </summary>
        private void UpdateOffsetsFromCreatedEffect_EditorOnly()
        {
            foreach (var effect in _effects)
            {
                if (effect.createdEffect == null) continue;

                if (effect.prev_createdLocalPosition != effect.createdEffect.localPosition)
                    effect.position = effect.createdEffect.localPosition;

                if (effect.prev_createdLocalRotation != effect.createdEffect.localEulerAngles)
                    effect.rotation = effect.createdEffect.localEulerAngles;

                if (effect.prev_createdLocalScale != effect.createdEffect.localScale)
                    effect.scale = effect.createdEffect.localScale;

                RememberPrevTransform(effect);
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

        private bool EffectArrayIsValid()
        {
            return _effects != null && _effects.Count > 0;
        }

        private bool EffectArrayIsNotValid()
        {
            return _effects == null || _effects.Count == 0;
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
        /// <summary> 특정 프레임에 이펙트 생성하기 </summary>
        private void SpawnEffect(EffectBundle effect)
        {
            if (effect.effectPrefab == null) return;

            // 기존에 생성된 이펙트가 있었으면 지워버리기
            if (effect.createdEffect != null)
            {
                Destroy(effect.createdEffect.gameObject);
            }

#if UNITY_EDITOR
            if (_stopAndEdit)
                edt_gapCount = 0;
#endif

            Transform effectTr = Instantiate(effect.effectPrefab).transform;

            if (effect.spawnAxis)
                effectTr.SetParent(effect.spawnAxis);

            effectTr.localPosition = effect.position;
            effectTr.localEulerAngles = effect.rotation;
            effectTr.localScale = effect.scale;

#if UNITY_EDITOR
            RememberPrevTransform(effect);
#endif

            // 현재 생성된 이펙트 트랜스폼 캐싱
            effect.createdEffect = effectTr;
        }

#if UNITY_EDITOR
        private void RememberPrevTransform(EffectBundle effect)
        {
            if (effect.createdEffect == null) return;

            effect.prev_createdLocalPosition = effect.createdEffect.localPosition;
            effect.prev_createdLocalRotation = effect.createdEffect.localEulerAngles;
            effect.prev_createdLocalScale = effect.createdEffect.localScale;
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
                if (target._effects != null && target._effects.Count > 0)
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
                if (target._effects != null && target._effects.Count > 0)
                {
                    string filePath = GetJsonFilePath(target.GetInstanceID().ToString());

                    if (File.Exists(filePath))
                    {
                        string jsonStr = File.ReadAllText(filePath);
                        SerializableAnimatorEventController restored = JsonUtility.FromJson<SerializableAnimatorEventController>(jsonStr);

                        restored.RestoreValues(target);

                        for (int i = 0; i < target._effects.Count; i++)
                        {
                            target._effects[i].createdEffect = null;
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
            public List<EffectBundle> effects;

            public SerializableAnimatorEventController(AnimatorEventController aec)
            {
                this.effects = aec._effects;
                this.stopAndEdit = aec._stopAndEdit;
                this.timeScaleNormal = aec._timeScale;
                this.timeScaleEdit = aec._timeScale_editMode;
            }

            public void RestoreValues(AnimatorEventController aec)
            {
                aec._effects = this.effects;
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
        private int edt_effectsArraySize = 0;
        private int edt_gapCount = edt_GapCountMax; // 이펙트 강제 생성 시 타임스케일 잠시 복원하는 동안 카운트
        private const int edt_GapCountMax = 1;
        private string edt_forceToStartAnimatorState = ""; // 에디터에서 강제로 특정 애니메이션 상태 실행

        [CustomEditor(typeof(AnimatorEventController))]
        private class CE : UnityEditor.Editor
        {
            private AnimatorEventController m;

            //private SerializedProperty _effects;

            private bool isHangle = false;
            private static readonly string HanglePrefsKey = "Rito_AEC_Hangle";

            private bool effectArrayFoldout = false;
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

            private int currentAnimClipIndex;

            private void OnEnable()
            {
                m = target as AnimatorEventController;

                if(m._animator == null)
                    m._animator = m.GetComponent<Animator>();

                //_effects = serializedObject.FindProperty(nameof(m._effects));

                _timeController = m; // 시간 조정 권한 양도

                isHangle = EditorPrefs.GetBool(HanglePrefsKey, false);
                effectArrayFoldout = EditorPrefs.GetBool(ArrayFoldoutPrefsKey, false);
                animationEventFoldout = EditorPrefs.GetBool(AnimationEventPrefsKey, false);
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

            private readonly List<EffectBundle> sortedEffectList = new List<EffectBundle>();
            private readonly StringBuilder sortedEffectInfoSb = new StringBuilder();
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

                EditorGUILayout.Space(8f);

                DrawEffectsArray();

                //EditorGUILayout.PropertyField(_effects);
                //serializedObject.ApplyModifiedProperties();
            }

            private void DrawAnimationEventList()
            {
                string eventFoldoutStr = EngHan("Animation Events", "애니메이션 이벤트 목록");

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
                        sortedEffectList.Clear();

                        m._effects.ForEach(eff =>
                        {
                            if (eff.effectPrefab != null && eff.animationClip == clip)
                                sortedEffectList.Add(eff);
                        });

                        if (sortedEffectList.Count == 0) continue;

                        sortedEffectInfoSb.Clear();
                        sortedEffectInfoSb.Append($"[{clip.name} (0 ~ {m.GetTotalFrameInt(clip)})]");

                        sortedEffectList.Sort((a, b) => a.spawnFrame - b.spawnFrame);
                        sortedEffectList.ForEach(eff =>
                        {
                            sortedEffectInfoSb.Append($"\n - {eff.spawnFrame:D3} : ").Append(eff.name);
                        });

                        EditorGUILayout.HelpBox(sortedEffectInfoSb.ToString(), MessageType.Info);
                    }

                    EditorGUILayout.Space(8f);
                }
            }

            private void DrawEngHanButton()
            {
                EditorGUILayout.Space(0f);
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

                EditorGUILayout.Space(8f);

                Color oldCol = GUI.color;

                if (!m._stopAndEdit) GUI.color = Color.cyan * 1.5f;
                m._timeScale = EditorGUILayout.Slider(EngHan("Time Scale(Normal)", "게임 진행 속도(일반 모드)"), m._timeScale, 0f, 1f);

                GUI.color = oldCol;
                if (m._stopAndEdit) GUI.color = Color.cyan * 1.5f;

                m._timeScale_editMode = EditorGUILayout.Slider(EngHan("Time Scale(Edit Mode)", "게임 진행 속도(편집 모드)"), m._timeScale_editMode, 0f, 1f);

                GUI.color = oldCol;

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(8f);

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

                EditorGUILayout.Space(8f);

                if (GUILayout.Button(EngHan("Restart from Beginning", "처음부터 다시 시작")))
                {
                    RemoveAllCreatedEffects();
                    m._currentFrameInt = 0;
                    m._animator.Play(0, 0, 0);
                }
                if (GUILayout.Button(EngHan("Remove All Instantiated Effects", "복제된 모든 이펙트 제거")))
                {
                    RemoveAllCreatedEffects();
                }

                void RemoveAllCreatedEffects()
                {
                    if (m._effects != null && m._effects.Count > 0)
                    {
                        for (int i = 0; i < m._effects.Count; i++)
                        {
                            if (m._effects[i].createdEffect != null)
                                Destroy(m._effects[i].createdEffect.gameObject);
                        }
                    }
                }
            }

            private void DrawEffectsArray()
            {
                bool oldFoldout = effectArrayFoldout;
                effectArrayFoldout = EditorGUILayout.Foldout(effectArrayFoldout, EngHan("Effects", "이펙트 목록"), true);

                if (oldFoldout != effectArrayFoldout)
                {
                    EditorPrefs.SetBool(ArrayFoldoutPrefsKey, effectArrayFoldout);
                }

                if (effectArrayFoldout)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    m.edt_effectsArraySize = EditorGUILayout.IntField(EngHan("Size", "개수"), m._effects.Count);
                    bool changed = EditorGUI.EndChangeCheck();

                    if (changed)
                    {
                        if (m.edt_effectsArraySize < 0)
                            m.edt_effectsArraySize = 0;

                        ref int newSize = ref m.edt_effectsArraySize;
                        int oldSize = m._effects.Count;

                        if (newSize < oldSize)
                        {
                            for (int i = oldSize - 1; i >= newSize; i--)
                            {
                                m._effects.RemoveAt(i);
                            }
                        }
                        else if (newSize > oldSize)
                        {
                            for (int i = oldSize; i < newSize; i++)
                            {
                                m._effects.Add(new EffectBundle());
                            }
                        }
                    }

                    bool addNew = GUILayout.Button("+", GUILayout.Width(40f));
                    if (addNew) m._effects.Add(new EffectBundle());

                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < m._effects.Count; i++)
                    {
                        DrawEffectBundle(m._effects[i], i);
                    }
                }


                EditorGUILayout.Space(8f);

                bool addNew2 = GUILayout.Button("+");
                if (addNew2) m._effects.Add(new EffectBundle());
            }

            private void DrawEffectBundle(EffectBundle effect, int index)
            {
                EditorGUILayout.Space(8f);

                string name = string.IsNullOrWhiteSpace(effect.name) ? EngHan($"Effect {index}", $"이펙트 {index}") : effect.name;

                EditorGUILayout.BeginHorizontal();

                Color oldGUIColor = GUI.color;
                GUI.color = effect.effectPrefab == null ? Color.red * 3f : Color.cyan;
                effect.edt_foldout = EditorGUILayout.Foldout(effect.edt_foldout, name, true);
                GUI.color = oldGUIColor;

                bool remove = GUILayout.Button("-", GUILayout.Width(40f));
                if (remove) m._effects.RemoveAt(index);

                EditorGUILayout.EndHorizontal();

                if (effect.edt_foldout)
                {
                    EditorGUI.indentLevel++;

                    string nameStr = EngHan("Name", "이름");
                    string animationClipStr = EngHan("Animation Clip", "애니메이션");
                    string spawnFrameStr = EngHan("Spawn Frame", "생성 프레임");
                    string prefabStr = EngHan("Effect Prefab", "이펙트 프리팹");
                    string axisStr = EngHan("Spawn Axis", "부모 트랜스폼");
                    string axisWarningMsg = EngHan("Cannot register prefab assets with Spawn Axis",
                        "프리팹 애셋은 부모 트랜스폼으로 설정될 수 없습니다.");
                    string createdEffectStr = EngHan("Created Effect", "생성된 이펙트");
                    string posStr = EngHan("Position", "위치");
                    string rotStr = EngHan("Rotation", "회전");
                    string scaleStr = EngHan("Scale", "크기");

                    string buttonStr_jumpToSpawnFrame = EngHan("Jump to Spawn Frame", "생성 프레임으로 이동");
                    string buttonStr_createNew = EngHan("Create", "이펙트 생성");
                    string buttonStr_remove = EngHan("Remove", "이펙트 제거");

                    // Name
                    effect.name = EditorGUILayout.TextField(nameStr, effect.name);

                    // Animation Clip - Dropdown
                    if (allClips?.Length > 0)
                    {
                        // 선택 가능한 클립이 2개 이상인 경우에만 드롭다운 제공
                        if(allClips.Length >= 2)
                            effect.animationClipIndex = EditorGUILayout.Popup(animationClipStr, effect.animationClipIndex, allClipStrings);

                        if (effect.animationClipIndex >= allClips.Length)
                        {
                            effect.animationClipIndex = 0;
                        }

                        effect.animationClip = allClips[effect.animationClipIndex];

                        // Spawn Frame
                        effect.spawnFrame = EditorGUILayout.IntSlider(spawnFrameStr, effect.spawnFrame, 0, m.GetTotalFrameInt(effect.animationClip));
                    }
                    else
                    {
                        effect.animationClip = null;
                        effect.animationClipIndex = 0;
                        effect.spawnFrame = 0;
                    }


                    // Button : Jump to Spawn Frame
                    if (Application.isPlaying)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(" ", GUILayout.Width(28f));
                        if (GUILayout.Button(buttonStr_jumpToSpawnFrame))
                        {
                            m._currentFrameInt = effect.spawnFrame;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space(8f);

                    // Effect Prefab
                    EditorGUI.BeginChangeCheck();

                    Color oldGUIColor2 = GUI.color;
                    GUI.color = effect.effectPrefab == null ? Color.red * 2f : Color.cyan * 2f;
                    effect.effectPrefab = EditorGUILayout.ObjectField(prefabStr, effect.effectPrefab, typeof(GameObject), true) as GameObject;
                    GUI.color = oldGUIColor2;

                    if (EditorGUI.EndChangeCheck())
                    {
                        // 프리팹 등록 또는 해제 시 자동 이름 설정
                        if (effect.effectPrefab == null)
                            effect.name = "";
                        else
                            effect.name = effect.effectPrefab.name;
                    }

                    // Spawn Axis
                    Transform prevAxis = effect.spawnAxis;

                    EditorGUI.BeginChangeCheck();
                    effect.spawnAxis = EditorGUILayout.ObjectField(axisStr, effect.spawnAxis, typeof(Transform), true) as Transform;
                    if (EditorGUI.EndChangeCheck())
                    {
                        // Spawn Axis에는 프리팹 못넣게 설정
                        if (effect.spawnAxis != null && PrefabUtility.IsPartOfPrefabAsset(effect.spawnAxis))
                        {
                            effect.spawnAxis = prevAxis;
                            EditorUtility.DisplayDialog("Rito", axisWarningMsg, "OK");
                        }
                    }

                    // Created Effect
                    if (Application.isPlaying)
                    {
                        Color oldCol = GUI.color;
                        if (effect.createdEffect != null)
                            GUI.color = Color.cyan * 1.5f;

                        EditorGUI.BeginDisabledGroup(true);
                        _ = EditorGUILayout.ObjectField(createdEffectStr, effect.createdEffect, typeof(GameObject), true) as GameObject;
                        EditorGUI.EndDisabledGroup();

                        GUI.color = oldCol;
                    }

                    EditorGUILayout.Space(2f);

                    // Buttons : Create, Remove
                    if (Application.isPlaying)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(" ", GUILayout.Width(28f));

                        if (GUILayout.Button(buttonStr_createNew))
                        {
                            m.SpawnEffect(effect);
                        }

                        if (GUILayout.Button(buttonStr_remove) && effect.createdEffect != null)
                        {
                            if(effect.createdEffect != null)
                                Destroy(effect.createdEffect.gameObject);
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space(8f);

                    // Pos, Rot, Scale
                    EditorGUI.BeginChangeCheck();

                    effect.position = EditorGUILayout.Vector3Field(posStr, effect.position);
                    effect.rotation = EditorGUILayout.Vector3Field(rotStr, effect.rotation);
                    effect.scale    = EditorGUILayout.Vector3Field(scaleStr, effect.scale);

                    if (EditorGUI.EndChangeCheck() && effect.createdEffect != null)
                    {
                        effect.createdEffect.localPosition = effect.position;
                        effect.createdEffect.localEulerAngles = effect.rotation;
                        effect.createdEffect.localScale = effect.scale;
                    }

                    EditorGUI.indentLevel--;
                }
            }

            private string EngHan(string eng, string han)
            {
                return !isHangle ? eng : han;
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