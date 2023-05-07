using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PixelIDControl : MonoBehaviour
{
    public uint ID;

    // Start is called before the first frame update
    void Start()
    {
        var render = GetComponent<Renderer>();
        render.sharedMaterial.SetFloat("_ID", ID);
    }

    // Update is called once per frame
    void Update()
    {
    }
}