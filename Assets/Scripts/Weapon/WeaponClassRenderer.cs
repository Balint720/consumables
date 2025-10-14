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

public partial class WeaponClass : MonoBehaviour
{
    LineRenderer lineRenderer;
    Timer lineTimer;

    protected bool LineRenderOn
    {
        get => lineRenderer.enabled;
        set => lineRenderer.enabled = value;
    }

    void LineRendererSetup()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;

        lineRenderer.positionCount = 10;
    }

    public static void DrawArcOfProj(LineRenderer lr, Vector3 origin, Vector3 dir, float speed, float gravity)
    {
        for (int i = 0; i < lr.positionCount; i++)
        {
            Vector3 position = origin + (dir * speed * Time.fixedDeltaTime * i) + (Vector3.down * gravity * Time.fixedDeltaTime * i);
            lr.SetPosition(i, position);
        }
    }

    public void DrawArcOfProjThis(CameraControl cam)
    {
        if (currStats.projectile == null) return;
        float speed = currStats.projSpeed > 0.0f ? currStats.projSpeed : currStats.projectile.Speed;
        float grav = currStats.projectile.Gravity;
        Vector3 origin = cam.transform.position + cam.transform.rotation * offset;
        Vector3 dir = Vector3.Normalize(origin + 50 * cam.transform.forward - origin);

        DrawArcOfProj(lineRenderer, origin, dir, speed, grav);
    }

}