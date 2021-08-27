#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

// 날짜 : 2021-08-28 AM 2:52:44
// 작성자 : Rito

namespace Rito
{
    public class Test_GradientFields : MonoBehaviour
    {
        [CustomEditor(typeof(Test_GradientFields))]
        private class CE : Editor
        {
            private Test_GradientFields m;

            private void OnEnable()
            {
                m = target as Test_GradientFields;
            }

            public override void OnInspectorGUI()
            {
                Gradient g = new Gradient();

                List<GradientColorKey> colorKeyList = new List<GradientColorKey>(8);
                colorKeyList.Add(new GradientColorKey(Color.white, 0f));
                colorKeyList.Add(new GradientColorKey(Color.black, 0.2f));
                colorKeyList.Add(new GradientColorKey(Color.blue, 0.5f));
                colorKeyList.Add(new GradientColorKey(Color.red, 1f));

                List<GradientAlphaKey> alphaKeyList = new List<GradientAlphaKey>(8);
                alphaKeyList.Add(new GradientAlphaKey(0f, 0f));
                alphaKeyList.Add(new GradientAlphaKey(1f, 0.5f));
                alphaKeyList.Add(new GradientAlphaKey(0.5f, 1f));

                g.colorKeys = colorKeyList.ToArray();
                g.alphaKeys = alphaKeyList.ToArray();

                EditorGUILayout.GradientField(g);
            }
        }
    }
}

#endif