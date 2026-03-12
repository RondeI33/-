using System;
using System.Collections.Generic;
using UnityEngine;

public class ShotData
{
    public Vector3 origin;
    public Vector3 direction;
    public float damage;
    public float speed;
    public float maxDistance;
    public bool isRaycast;
    public GameObject projectilePrefab;
    public int recursionDepth;
    public int maxRecursionDepth = 5;
    public WeaponController weaponController;
    public LayerMask hitLayers;
    public List<Action<HitInfo, ShotData>> onHitCallbacks = new List<Action<HitInfo, ShotData>>();
    public List<Action<HitInfo?, ShotData>> onPostExecute = new List<Action<HitInfo?, ShotData>>();
    public Dictionary<string, object> properties = new Dictionary<string, object>();
    public HashSet<int> appliedModifiers = new HashSet<int>();

    public ShotData Clone()
    {
        return new ShotData
        {
            origin = origin,
            direction = direction,
            damage = damage,
            speed = speed,
            maxDistance = maxDistance,
            isRaycast = isRaycast,
            projectilePrefab = projectilePrefab,
            recursionDepth = recursionDepth,
            maxRecursionDepth = maxRecursionDepth,
            weaponController = weaponController,
            hitLayers = hitLayers,
            onHitCallbacks = new List<Action<HitInfo, ShotData>>(onHitCallbacks),
            onPostExecute = new List<Action<HitInfo?, ShotData>>(onPostExecute),
            properties = new Dictionary<string, object>(properties),
            appliedModifiers = new HashSet<int>(appliedModifiers)
        };
    }

    public T GetProperty<T>(string key, T fallback = default)
    {
        if (properties.TryGetValue(key, out object value) && value is T typed)
            return typed;
        return fallback;
    }

    public void SetProperty(string key, object value)
    {
        properties[key] = value;
    }

    public bool HasAppliedModifier(int id)
    {
        return appliedModifiers.Contains(id);
    }

    public void MarkModifierApplied(int id)
    {
        appliedModifiers.Add(id);
    }
}
