using Assets.Scripts.Neat;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PreyController))]
public class FieldOfViewEditor : Editor
{
    void OnSceneGUI()
    {
        PreyController fow = (PreyController)target;
        Handles.color = Color.white;
        Handles.DrawWireArc(fow.transform.position, Vector3.forward, Vector3.right, 360, fow.viewDistance);
        Vector3[] splitLines = new Vector3[fow.angleSplits + 1];
        float angleIncrement = fow.viewAngle / fow.angleSplits;
        float limitLeft = fow.viewAngle / 2;

        for (int i = 0; i < splitLines.Length; i++)
        {
            splitLines[i] = fow.DirFromAngle(limitLeft - (angleIncrement * i), false);
            Handles.DrawLine(fow.transform.position, fow.transform.position + -(splitLines[i]) * fow.viewDistance);
        }

        //for(int i = 0; i < fow.fowTargets.Count; i++)
        //{
        //    (var r, var g, var b) = Utils.CreateColorFromId(i + 3);
        //    Color c = new Color32(r, g, b, 255);
        //    Handles.color = c;
        //    foreach(var target in fow.fowTargets[i])
        //    {
        //        Handles.DrawLine(fow.transform.position, target.position);
        //    }
        //}
    }
}
