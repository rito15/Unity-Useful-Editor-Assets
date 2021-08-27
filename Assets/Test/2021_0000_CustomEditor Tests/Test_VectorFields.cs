#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

// 날짜 : 2021-08-27 PM 10:39:34
// 작성자 : Rito

namespace Rito
{
    public class Test_VectorFields : MonoBehaviour
    {
        [CustomEditor(typeof(Test_VectorFields))]
        private class CE : UnityEditor.Editor
        {
            private Test_VectorFields m;

            private void OnEnable()
            {
                m = target as Test_VectorFields;
                Debug.Log(Application.dataPath);

                string str = Application.dataPath;
                Debug.Log(str.Substring(0, str.LastIndexOf('/')));
            }

            private Vector2 vec2;
            private Vector3 vec3;
            private Vector4 vec4;

            private static FieldInfo fiVector4FieldLables;
            private static GUIContent[] vector4FieldLables;

            public override void OnInspectorGUI()
            {
                BindingFlags privateStatic = BindingFlags.Static | BindingFlags.NonPublic;

                // Vector4 필드의 XYZW 레이블
                if (fiVector4FieldLables == null)
                {
                    fiVector4FieldLables = typeof(EditorGUI).GetField("s_XYZWLabels", privateStatic);
                    vector4FieldLables = fiVector4FieldLables.GetValue(null) as GUIContent[];
                }


                // [1] Vector2 : X, Y -> A, B로 변경
                vector4FieldLables[0].text = "A";
                vector4FieldLables[1].text = "B";

                vec2 = EditorGUILayout.Vector2Field("Vec2", vec2);


                // [2] Vector3 : X, Y, Z -> ㄴ ㅇ ㄱ 으로 변경
                vector4FieldLables[0].text = "ㄴ";
                vector4FieldLables[1].text = "ㅇ";
                vector4FieldLables[2].text = "ㄱ";

                vec3 = EditorGUILayout.Vector3Field("Vec3", vec3);


                // [3] Vector4 : X, Y, Z, W -> R, G, B, A로 변경
                vector4FieldLables[0].text = "R";
                vector4FieldLables[1].text = "G";
                vector4FieldLables[2].text = "B";
                vector4FieldLables[3].text = "A";

                vec4 = EditorGUILayout.Vector4Field("Vec4", vec4);


                // X, Y, Z, W 레이블 복원
                vector4FieldLables[0].text = "X";
                vector4FieldLables[1].text = "Y";
                vector4FieldLables[2].text = "Z";
                vector4FieldLables[3].text = "W";
            }
        }
    }
}

#endif