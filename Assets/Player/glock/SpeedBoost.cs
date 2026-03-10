using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    private float boostMultiplier = 1.33f;
    private FirstPersonController fpsMovement;

    private void Awake()
    {
        fpsMovement = GetComponentInParent<FirstPersonController>();
    }

    private void OnEnable()
    {
        if (fpsMovement) fpsMovement.SetSpeedMultiplier(boostMultiplier);
    }

    private void OnDisable()
    {
        if (fpsMovement) fpsMovement.SetSpeedMultiplier(1f);
    }
}