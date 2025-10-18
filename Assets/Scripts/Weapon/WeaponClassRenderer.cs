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
    LineRenderer[] lrs;
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
                //if (value) StartCoroutine(interpolateLinesToWeaponRoutine);
                //else StopCoroutine(interpolateLinesToWeaponRoutine);

                linesParent.SetActive(value);
            }
        }
    }

    const int numOfPointsInLine = 20;
    const float sizeOfCutOff = 0.2f;
    const float increment = 10.0f;

    IEnumerator interpolateLinesToWeaponRoutine;

    float timeOfInterpolation = 0.0f;
    static float oneFullInterpolation = 10.0f;
    float t = 0.0f;

    public void SetUpLineGameObjects()
    {
        linesParent = new GameObject("Lines");
        linesParent.transform.SetParent(transform);
        lines = new GameObject[numOfPointsInLine];
        lrs = new LineRenderer[numOfPointsInLine];

        for (int i = 0; i < lines.Count(); i++)
        {
            lines[i] = new GameObject();
            lines[i].transform.parent = linesParent.transform;
            lrs[i] = new LineRenderer();
            lrs[i] = lines[i].AddComponent<LineRenderer>();
            lrs[i].material = new Material(Shader.Find("Sprites/Default"));
            lrs[i].positionCount = 2;
            lrs[i].startColor = Color.white;
            lrs[i].endColor = Color.red;
            lrs[i].startWidth = 0.2f;
            lrs[i].endWidth = 0.2f;
        }

        interpolateLinesToWeaponRoutine = InterpolateLinesPosition();
        LinesEnabled = false;
    }

    public void DrawArcOfProj(Vector3 origin, Vector3 dir, float speed, float gravity)
    {
        Vector3 speedVec = dir * speed;
        Vector3 prevPos = origin;
        for (int i = 1; i < numOfPointsInLine; i++)
        {
            Vector3 position = prevPos + speedVec * Time.fixedDeltaTime;
            Vector3 newPos = Vector3.Lerp(lrs[i].GetPosition(0), prevPos + speedVec * Time.fixedDeltaTime * sizeOfCutOff, 10.0f * Time.deltaTime);
            Vector3 newPrevPos = Vector3.Lerp(lrs[i].GetPosition(1), position - speedVec * Time.fixedDeltaTime * sizeOfCutOff, 10.0f * Time.deltaTime);
            //Vector3 newPos = Vector3.MoveTowards(lrs[i].GetPosition(0), prevPos + speedVec * Time.fixedDeltaTime * sizeOfCutOff, increment * Time.fixedDeltaTime);
            //Vector3 newPrevPos = Vector3.MoveTowards(lrs[i].GetPosition(1), position - speedVec * Time.fixedDeltaTime * sizeOfCutOff, increment * Time.fixedDeltaTime);
            lrs[i].SetPosition(0, newPos);
            lrs[i].SetPosition(1, newPrevPos);
            prevPos = position;
            speedVec += gravity * Time.fixedDeltaTime * Vector3.down;
        }
    }

    public void DrawArcOfProjTrans(Transform t)
    {
        if (currStats.projectile == null) return;
        float speed = currStats.projSpeed > 0.0f ? currStats.projSpeed : currStats.projectile.Speed;
        float grav = currStats.projectile.Gravity;
        Vector3 origin = t.transform.position + t.transform.rotation * offset;
        Vector3 dir = Vector3.Normalize(origin + distanceFromOriginToConvergeTo * t.transform.forward - origin);

        DrawArcOfProj(origin, dir, speed, grav);
    }

    void UpdateDrawArcOfBow()
    {
        if (LinesEnabled = t_state == TriggerState.HELD && currStats.fireMode == FiringMode.BOW)
        {
            // Draw arc of projectile
            Vector3 origin = shootPos.position;
            Vector3 direction = shootPos.forward;

            RaycastHit hitInfo = ShootHitScan(origin, direction);
            Vector3 projOffsetOrigin = origin + shootPos.rotation * new Vector3(offset.x, offset.y, offset.z);
            Vector3 projOffsetDir = Vector3.Normalize(origin + distanceFromOriginToConvergeTo * direction - projOffsetOrigin);
            if (hitInfo.collider != null)
            {
                projOffsetDir = Vector3.Normalize(hitInfo.point - projOffsetOrigin);
            }

            DrawArcOfProj(projOffsetOrigin, projOffsetDir, currStats.projSpeed > 0.0f ? currStats.projSpeed * projSpeedMod * ChargeMod : currStats.projectile.Speed * projSpeedMod * ChargeMod, currStats.projectile.Gravity);
        }
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