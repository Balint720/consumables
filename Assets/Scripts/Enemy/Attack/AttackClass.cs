using System;
using System.Collections.Generic;
using UnityEngine;

// Use the CreateAssetMenu attribute to allow creating instances of this ScriptableObject from the Unity Editor.
[CreateAssetMenu(fileName = "Attack", menuName = "ScriptableObjects/Attack", order = 1)]
public class AttackClass : ScriptableObject
{
    [SerializeField] int damage;
    [SerializeField] float knockback;
    [SerializeField] List<string> hurtBoxes;
    [SerializeField] List<Animation[]> animations;
    [SerializeField] List<int[]> activeInactiveFrames;

}
