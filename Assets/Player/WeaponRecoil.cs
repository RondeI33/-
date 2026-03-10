using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponRecoil : MonoBehaviour
{
    public float recoilDistance = 0.08f;
    public float recoilRotation = 3f;
    public Vector3 recoilRotationAxis = new Vector3(-1f, 0f, 0f);
    public Vector3 recoilPushAxis = new Vector3(0f, 0f, -1f);
    public float recoilSpeed = 20f;
    public float recoverySpeed = 6f;
    public float rotationDelay = 0.05f;
    public bool fullAuto = false;
    public float fireRate = 10f;
    public bool useCooldown = false;
    public float cooldownTime = 0.5f;
    public int maxAmmo = 6;
    public AnimationClip reloadClip;
    public string reloadAnimationName = "reload";
    public float reloadSpeed = 2f;
    public int ammoPerSpin = 1;
    public GameObject dropPrefab;
    public System.Action OnFired;
    public System.Action OnAmmoEmpty;

    private PlayerInput playerInput;
    private InputAction attackAction;
    private Vector3 currentRecoil;
    private Vector3 targetRecoil;
    private Vector3 currentRotationRecoil;
    private Vector3 targetRotationRecoil;
    private Vector3 pendingRotationRecoil;
    private float rotationDelayTimer;
    private float fireTimer;
    private float cooldownTimer;
    private int currentAmmo;
    private bool reloadBlocked;
    private bool waitingForRelease;
    private bool reloadJustEnded;

    private float baseFireRate;
    private float baseCooldownTime;
    private bool speedBuffed;
    private bool basesCaptured;

    private void OnEnable()
    {
        if (!basesCaptured)
        {
            baseFireRate = fireRate;
            baseCooldownTime = cooldownTime;
            basesCaptured = true;
        }
    }

    private void Awake()
    {
        if (!basesCaptured)
        {
            baseFireRate = fireRate;
            baseCooldownTime = cooldownTime;
            basesCaptured = true;
        }
    }

    private void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        attackAction = playerInput.actions["Attack"];
        currentAmmo = maxAmmo;
    }

    private void Update()
    {
        if (reloadJustEnded)
        {
            reloadJustEnded = false;
            return;
        }

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        bool fired = false;
        if (currentAmmo > 0 && !reloadBlocked)
        {
            if (useCooldown)
            {
                if (attackAction.IsPressed() && cooldownTimer <= 0f)
                {
                    fired = true;
                    cooldownTimer = cooldownTime;
                }
            }
            else if (fullAuto)
            {
                if (attackAction.IsPressed())
                {
                    fireTimer -= Time.deltaTime;
                    if (fireTimer <= 0f)
                    {
                        fired = true;
                        fireTimer = 1f / fireRate;
                    }
                }
                else
                {
                    fireTimer = 0f;
                }
            }
            else
            {
                fired = attackAction.WasPressedThisFrame();
            }
        }

        if (fired)
        {
            currentAmmo--;
            targetRecoil = recoilPushAxis.normalized * recoilDistance;
            pendingRotationRecoil = recoilRotationAxis * recoilRotation;
            rotationDelayTimer = rotationDelay;
            OnFired?.Invoke();

            if (currentAmmo <= 0 && !reloadBlocked)
                OnAmmoEmpty?.Invoke();
        }

        if (!waitingForRelease && currentAmmo <= 0 && !reloadBlocked && attackAction.WasPressedThisFrame())
            waitingForRelease = true;

        if (waitingForRelease && !attackAction.IsPressed())
        {
            waitingForRelease = false;
            if (!reloadBlocked)
                OnAmmoEmpty?.Invoke();
        }

        if (rotationDelayTimer > 0f)
        {
            rotationDelayTimer -= Time.deltaTime;
            if (rotationDelayTimer <= 0f)
                targetRotationRecoil = pendingRotationRecoil;
        }

        float posSpeed = targetRecoil.sqrMagnitude > currentRecoil.sqrMagnitude ? recoilSpeed : recoverySpeed;
        currentRecoil = Vector3.Lerp(currentRecoil, targetRecoil, Time.deltaTime * posSpeed);

        float rotSpeed = targetRotationRecoil.sqrMagnitude > currentRotationRecoil.sqrMagnitude ? recoilSpeed : recoverySpeed;
        currentRotationRecoil = Vector3.Lerp(currentRotationRecoil, targetRotationRecoil, Time.deltaTime * rotSpeed);

        if (targetRecoil != Vector3.zero && (currentRecoil - targetRecoil).sqrMagnitude < 0.000001f)
            targetRecoil = Vector3.zero;

        if (targetRotationRecoil != Vector3.zero && (currentRotationRecoil - targetRotationRecoil).sqrMagnitude < 0.000001f)
            targetRotationRecoil = Vector3.zero;
    }

    public void ApplySpeedBuff(float fireRateMultiplier, float cooldownMultiplier)
    {
        EnsureBasesCaptured();
        if (fullAuto) fireRate = baseFireRate * fireRateMultiplier;
        if (useCooldown) cooldownTime = baseCooldownTime * cooldownMultiplier;
        speedBuffed = true;
    }

    public void RemoveSpeedBuff()
    {
        EnsureBasesCaptured();
        if (fullAuto) fireRate = baseFireRate;
        if (useCooldown) cooldownTime = baseCooldownTime;
        speedBuffed = false;
    }

    private void EnsureBasesCaptured()
    {
        if (basesCaptured) return;
        baseFireRate = fireRate;
        baseCooldownTime = cooldownTime;
        basesCaptured = true;
    }

    public bool IsSpeedBuffed() => speedBuffed;

    public void SetReloadBlocked(bool blocked)
    {
        reloadBlocked = blocked;
        if (!blocked)
        {
            
            cooldownTimer = 0f;
            reloadJustEnded = true;
        }
    }

    public void SetAmmo(int ammo) => currentAmmo = Mathf.Clamp(ammo, 0, maxAmmo);
    public void AddAmmo(int amount) => currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
    public AnimationClip GetReloadClip() => reloadClip;
    public string GetReloadAnimationName() => reloadAnimationName;
    public float GetReloadSpeed() => reloadSpeed;
    public int GetAmmoPerSpin() => ammoPerSpin;
    public bool IsReloading() => reloadBlocked;
    public Vector3 GetRecoilOffset() => currentRecoil;
    public Quaternion GetRecoilRotation() => Quaternion.Euler(currentRotationRecoil);
}