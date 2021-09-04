#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System;

// 날짜 : 2021-09-03 PM 8:41:38
// 작성자 : Rito


using UnityEngine;
using UnityEditor;

public class Test_RainbowButton : MonoBehaviour
{
    [CustomEditor(typeof(Test_RainbowButton))]
    private class CE : Editor
    {
        private float hue = 0f;
        private float delta = 0.001f;

        public override void OnInspectorGUI()
        {
            Color oldBG = GUI.backgroundColor;
            GUI.backgroundColor = Color.HSVToRGB(hue, 1f, 2f);

            GUILayout.Button(" ", GUILayout.Height(40f));
            GUILayout.Button(" ", GUILayout.Height(40f));
            GUILayout.Button(" ", GUILayout.Height(40f));

            hue = (hue += delta) % 1.0f;
            Repaint();

            GUI.backgroundColor = oldBG;
        }
    }
}

#endif