#if UNITY_EDITOR

#define CREATE_MENU_ITEM_
#define CREATE_TOOLBAR_BUTTON

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityToolbarExtenderTSS;
using UnityEditor.SceneManagement;

// 날짜 : 2021-06-30 PM 8:35:28
// 작성자 : Rito

namespace Rito
{
    /// <summary> 게임 뷰 실시간으로 업데이트하기 </summary>
    [InitializeOnLoad]
    public static class GameWindowAutoUpdater
    {
        /***********************************************************************
        *                               Menu Item
        ***********************************************************************/
        #region .
        private const string MenuItemTitle = "Window/Rito/Auto Update Game Window";

        private static bool MenuItemChecked
        {
            get => EditorPrefs.GetBool(MenuItemTitle, true);
            set
            {
                EditorPrefs.SetBool(MenuItemTitle, value);
                IsActivated = value;
            }
        }

        public static bool IsActivated { get; private set; }

#if CREATE_MENU_ITEM
        [MenuItem(MenuItemTitle, false)]
        private static void MenuItem()
        {
            // 체크 상태 변경은 메뉴아이템 메소드에서 수행
            MenuItemChecked = !MenuItemChecked;
        }

        [MenuItem(MenuItemTitle, true)]
        private static bool MenuItem_Validate()
        {
            // 체크 상태 갱신은 Validate 메소드에서 수행
            Menu.SetChecked(MenuItemTitle, MenuItemChecked);
            return true;
        }
#endif

        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            IsActivated = MenuItemChecked;
            EditorApplication.update -= UpdateGameWindow;
            EditorApplication.update += UpdateGameWindow;
        }

        private static void UpdateGameWindow()
        {
            if (!IsActivated) return;

            var target = GameObject.FindObjectOfType<Transform>();
            EditorUtility.SetDirty(target);
        }
        #endregion
        /***********************************************************************
        *                               Hierarchy Key Event
        ***********************************************************************/
        #region .

        // 하이라키의 게임오브젝트가 선택된 상태에서 F2 누를 경우 Auto Update 기능 해제
        [InitializeOnLoadMethod]
        private static void StaticInitMethod()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindow;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindow;
        }

        private static void OnHierarchyWindow(int instanceID, Rect selectionRect)
        {
            Event cur = Event.current;

            if (cur == null) return;
            if (IsActivated == false) return;
            if (Selection.activeGameObject == null) return;

            if (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.F2)
            {
                MenuItemChecked = false;
            }
        }

        #endregion
        /***********************************************************************
        *                               Toolbar Button
        ***********************************************************************/
        #region .
#if CREATE_TOOLBAR_BUTTON
        static GameWindowAutoUpdater()
        {
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
        }

        static void OnToolbarGUI()
        {
            if (EditorApplication.isPlaying) return;

            GUILayout.FlexibleSpace();

            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = IsActivated ? Color.green * 2f : Color.white;

            if (GUILayout.Button(new GUIContent("Auto Update Game View", ""), 
                EditorStyles.toolbarButton))
            {
                MenuItemChecked = !IsActivated;

                if (!IsActivated)
                    EditorSceneManager.SaveOpenScenes();
            }

            GUI.backgroundColor = bgColor;
        }
#endif
        #endregion
    }
} 

#endif