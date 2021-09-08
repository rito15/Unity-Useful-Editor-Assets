#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

// 날짜 : 2021-09-09 AM 2:09:09
// 작성자 : Rito

namespace Rito
{
    /// <summary> 
    /// 깔끔하고 모던한 스타일의 에디터 GUI 기능 제공
    /// </summary>
    public static class NeatEditorGUILayout
    {
        // Note : Dark 테마 여부 고려

        public static Color HeaderColor { get; set; } = new Color(0.05f, 0.05f, 0.05f);
        public static Color BoxColor { get; set; } = new Color(0.15f, 0.15f, 0.15f);
        public static Color OutlineColor { get; set; } = Color.black;
        public static Color HeaderTextColor { get; set; } = Color.white;

        // Box(int lineCount, )

        // HeaderBox(int lineCount, )

        // FoldoutHeaderBox(ref foldout, int lineCount, )

        // VerticalSpace
        public static void VerticalSpace(float height)
        {
            GUILayoutUtility.GetRect(1f, height);
        }

        // HorizontalSpace
        public static void HorizontalSpace(float width)
        {
            GUILayoutUtility.GetRect(width, 1f);
        }

        // Button
        // 블랙/화이트, 화이트/블랙 각각 편리하게 제공
        // 텍스트/배경 색상 직접 지정할 수 있는 메소드도 제공
        // 버튼 기본 높이는 20f로 고정


        // 자주 사용되는 필드들 깔끔하고 모던하고 예쁜 기본 테마로 제공(블랙&화이트)
        // 레이블/필드 영역 너비 비율 설정할 수 있도록 매개변수 옵션 제공 (Prefix레이블 쓰거나 리플렉션)

        // Dictionary Field
    }

    public class StyleSet
    {
        public readonly Color textColor;
        public readonly Color backgroundColor;
        public readonly int fontSize = 12;
        public readonly FontStyle fontStyle;
        public readonly TextAlignment align;

        public static GUIStyle BlackWhiteButton
        {
            get
            {
                if (_blackWhiteButton == null)
                {
                    _blackWhiteButton = new GUIStyle(GUI.skin.button);

                }
                return _blackWhiteButton;
            }
        }
        private static GUIStyle _blackWhiteButton;
    }
}

#endif