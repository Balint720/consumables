using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

// Wrapper classes
[System.Serializable]
public class ListWrapper<T> : IList<T>
{
    public List<T> innerList;

    public int Count => innerList.Count;
    public int Capacity => innerList.Capacity;

    public bool IsReadOnly => ((ICollection<T>)innerList).IsReadOnly;

    public ListWrapper()
    {
        innerList = new List<T>();
    }

    public T this[int key]
    {
        get { return innerList[key]; }
        set {innerList[key] = value; }
    }

    public void Add(T value)
    {
        innerList.Add(value);
    }

    public void RemoveAt(int index)
    {
        innerList.RemoveAt(index);
    }

    public int IndexOf(T item)
    {
        return innerList.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        innerList.Insert(index, item);
    }

    public void Clear()
    {
        ((ICollection<T>)innerList).Clear();
    }

    public bool Contains(T item)
    {
        return ((ICollection<T>)innerList).Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ((ICollection<T>)innerList).CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        return ((ICollection<T>)innerList).Remove(item);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)innerList).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)innerList).GetEnumerator();
    }
}


// Use the CreateAssetMenu attribute to allow creating instances of this ScriptableObject from the Unity Editor.
[CreateAssetMenu(fileName = "Attack", menuName = "ScriptableObjects/Attack", order = 1)]
public class AttackClass : ScriptableObject
{
    public int damage;
    public float knockback;
    public List<string> hurtboxes;
    [SerializeField] public List<ListWrapper<AnimationClip>> animations;
    public AnimationClip testAnimation;
    public List<ListWrapper<int>> activeInactiveFrames;

}
