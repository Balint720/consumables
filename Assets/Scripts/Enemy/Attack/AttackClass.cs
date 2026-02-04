using System;
using System.Collections.Generic;
using UnityEngine;

// Use the CreateAssetMenu attribute to allow creating instances of this ScriptableObject from the Unity Editor.
[CreateAssetMenu(fileName = "Attack", menuName = "ScriptableObjects/Attack", order = 1)]
public class AttackClass : ScriptableObject
{
    public int damage;
    public float knockback;
    public List<string> hurtboxes;
    public List<Animation[]> animations;
    public List<int[]> activeInactiveFrames;

}
