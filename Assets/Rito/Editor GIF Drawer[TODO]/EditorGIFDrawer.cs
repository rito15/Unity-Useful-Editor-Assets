#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Drawing;

// 날짜 : 2021-09-04 PM 3:49:03
// 작성자 : Rito

namespace Rito
{
    /// <summary> 
    /// 
    /// </summary>
    public class EditorGIFDrawer : MonoBehaviour
    {
        private void Start()
        {
            
        }
    }

    public class AnimatedGIF : ScriptableObject
    {
        public int frameCount;
        public List<Texture2D> imageList;
        public float speed;

        ~AnimatedGIF()
        {
            
        }
    }

    
}

#endif