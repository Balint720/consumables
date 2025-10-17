using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public partial class WeaponClass : MonoBehaviour
{
    [SerializeField] Image crossHair;
    Timer lineTimer;
    GameObject linesParent;
    GameObject[] lines;
    bool linesEnabled;
    public bool LinesEnabled
    {
        get
        {
            return linesEnabled;
        }
        set
        {
            if (value != linesEnabled)
            {
                if (value) StartCoroutine(interpolateLinesToWeaponRoutine);
                else StopCoroutine(interpolateLinesToWeaponRoutine);
            }

            foreach (LineRenderer lr in linesParent.GetComponentsInChildren<LineRenderer>())
            {
                lr.enabled = value;
            }
        }
    }

    const int numOfPointsInLine = 20;
    const float sizeOfCutOff = 0.2f;
    const float increment = 0.1f;

    IEnumerator interpolateLinesToWeaponRoutine;

    float timeOfInterpolation = 0.0f;
    static float oneFullInterpolation = 10.0f;
    float t = 0.0f;

    public void SetUpLineGameObjects()
    {
        linesParent = new GameObject("Lines");
        linesParent.transform.SetParent(transform);
        lines = new GameObject[numOfPointsInLine];

        for (int i = 0; i < lines.Count(); i++)
        {
            lines[i] = new GameObject();
            lines[i].transform.parent = linesParent.transform;
            LineRenderer lr = lines[i].AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = 2;
            lr.enabled = false;
            lr.startColor = Color.white;
            lr.endColor = Color.red;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.2f;
        }

        interpolateLinesToWeaponRoutine = InterpolateLinesPosition();
        LinesEnabled = true;
    }

    public void DrawArcOfProj(Vector3 origin, Vector3 dir, float speed, float gravity)
    {
        Vector3 speedVec = dir * speed;
        Vector3 prevPos = origin;
        for (int i = 1; i < numOfPointsInLine; i++)
        {
            Vector3 position = prevPos + speedVec * Time.fixedDeltaTime;
            LineRenderer lr = lines[i].GetComponent<LineRenderer>();
            //Vector3 newPos = Vector3.Lerp(lr.GetPosition(0), prevPos + speedVec * Time.fixedDeltaTime * sizeOfCutOff, t);
            //Vector3 newPrevPos = Vector3.Lerp(lr.GetPosition(1), position - speedVec * Time.fixedDeltaTime * sizeOfCutOff, t);
            Vector3 newPos = Vector3.MoveTowards(lr.GetPosition(0), prevPos + speedVec * Time.fixedDeltaTime * sizeOfCutOff, increment);
            Vector3 newPrevPos = Vector3.MoveTowards(lr.GetPosition(1), position - speedVec * Time.fixedDeltaTime * sizeOfCutOff, increment);
            lr.SetPosition(0, newPos);
            lr.SetPosition(1, newPrevPos);
            prevPos = position;
            speedVec += gravity * Time.fixedDeltaTime * Vector3.down;
        }
    }

    public void DrawArcOfProjCam(CameraControl cam)
    {
        if (currStats.projectile == null) return;
        float speed = currStats.projSpeed > 0.0f ? currStats.projSpeed : currStats.projectile.Speed;
        float grav = currStats.projectile.Gravity;
        Vector3 origin = cam.transform.position + cam.transform.rotation * offset;
        Vector3 dir = Vector3.Normalize(origin + distanceFromOriginToConvergeTo * cam.transform.forward - origin);

        DrawArcOfProj(origin, dir, speed, grav);
    }

    IEnumerator InterpolateLinesPosition()
    {
        while(true)
        {
            t = timeOfInterpolation / oneFullInterpolation;

            timeOfInterpolation = (timeOfInterpolation + Time.deltaTime) % oneFullInterpolation;

            yield return new WaitForNextFrameUnit();
        }
    }

}