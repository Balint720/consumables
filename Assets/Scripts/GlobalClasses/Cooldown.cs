using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class Cooldown
{
    protected float timer;
    public float timerMax;
    float cdMod;
    protected bool isReady;
    protected float lastTimeUsed;
    static float onSpawnDelay = 2.0f;
    public void Init()
    {
        cdMod = 1.0f;
        timer = onSpawnDelay;
        isReady = false;
    }
    public void ModifyCooldown(float timeOrMod, bool isTime = true)
    {
        if (isTime)
        {
            cdMod = timeOrMod / timerMax;
        }
        else
        {
            cdMod = timeOrMod;
        }
    }
    public void ResetTimer()
    {
        timer = timerMax;
    }
    public void UseCooldown()
    {
        isReady = false;
        lastTimeUsed = Time.time;
        ResetTimer();
    }
    public float GetTimer()
    {
        return timer;
    }
    public float GetLastTimeUsed()
    {
        return lastTimeUsed;
    }
    public bool IsReady()
    {
        return isReady;
    }
    public void CallPerFrame(float timeSinceLastFrame)
    {
        if (!isReady)
        {
            timer -= timeSinceLastFrame * cdMod;
            if (timer <= 0.0f)
            {
                isReady = true;
            }
        }
    }
}