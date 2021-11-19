using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Rito;

// 날짜 : 2021-10-13 PM 7:53:41
// 작성자 : Rito

/// <summary> 
/// 
/// </summary>
public class Test_VignetteController : MonoBehaviour
{
    public ScreenEffect effect;

    [Range(0, 2)]
    public float sightValue;

    private void Update()
    {
        effect.effectMaterial.SetFloat("_SightRange", sightValue);
    }
}