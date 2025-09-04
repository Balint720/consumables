using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class PatrolScript : MonoBehaviour
{
    List<GameObject> users;
    Dictionary<int, Transform> points;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Initialize variables
        users = new List<GameObject>();
        points = new Dictionary<int, Transform>();

        // Get patrol route points
        foreach (Transform tf in transform)
        {
            if (tf.name.Contains("Point"))
            {
                if (Int32.TryParse(tf.name.Substring(6), out int i))
                {
                    points.Add(i, tf);
                }
            }
        }
    }

    void FixedUpdate()
    {
        foreach (GameObject go in users)
        {
            if (!go)
            {
                users.Remove(go);
            }
        }
    }

    public bool GetPoint(int i, out Transform p)
    {
        return points.TryGetValue(i + 1, out p);
    }

    public bool GetPoints(int i, out Transform[] ps)
    {
        ps = new Transform[points.Count()];

        foreach (KeyValuePair<int, Transform> dict in points)
        {
            try
            {
                points[dict.Key - 1] = dict.Value;
            }
            catch (Exception e)
            {
                Debug.Log(e);
                Debug.Log("Array index issue: are the points in patrol route numbered from 1 to highest or are there any numbers missing?");
                return false;
            }
        }
        return true;
    }

    public int GetNumOfPoints()
    {
        return points.Count();
    }

    public bool IsUnused()
    {
        if (users != null)
        {
            return users.Count() <= 0 ? true : false;
        }
        else
        {
            return true;
        }
    }

    public void AddUser(GameObject go)
    {
        users.Add(go);
    }
}
