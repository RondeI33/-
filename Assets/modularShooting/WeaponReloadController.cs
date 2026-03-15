using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class WeaponReloadController : MonoBehaviour
{
    [Header("Ammo")]
    [SerializeField] int maxAmmo = 6;
    [SerializeField] TMP_Text ammoText;

    [Header("Reload")]
    [SerializeField] float reloadDuration = 1.5f;
    [SerializeField] Animation reloadAnimation;
    [SerializeField] AnimationClip reloadClip;
    [SerializeField] string reloadAnimationName = "reload";
    [SerializeField] float reloadAnimationSpeed = 1f;

    [Header("Reload Dip")]
    [SerializeField] Transform dipTarget;
    [SerializeField] float reloadDipAmount = -0.15f;
    [SerializeField] float reloadDipSpeed = 6f;
    [SerializeField] float reloadDipReturnSpeed = 4f;

    private WeaponController weaponController;
    private PlayerInput playerInput;
    private InputAction reloadAction;
    private InputAction attackAction;

    private int currentAmmo;
    private bool reloading;
    private float reloadTimer;

    private bool reloadDipping;
    private bool reloadDipReturning;
    private float reloadDipOffset;
    private Vector3 dipTargetStartPos;

    public System.Action OnAmmoEmpty;
    public System.Action OnAmmoChanged;
    public static System.Action<int, int> OnAnyAmmoChanged;
    public static System.Action<int> OnAnyReloadStart;

    void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        playerInput = GetComponentInParent<PlayerInput>();
    }

    void OnEnable()
    {
        weaponController.OnFired += HandleFired;
        reloadAction = playerInput.actions["Reload"];
        attackAction = playerInput.actions["Attack"];
    }

    void OnDisable()
    {
        weaponController.OnFired -= HandleFired;
    }

    void Start()
    {
        currentAmmo = maxAmmo;

        if (dipTarget != null)
            dipTargetStartPos = dipTarget.localPosition;

        if (reloadAnimation != null && reloadClip != null)
        {
            reloadAnimation.AddClip(reloadClip, reloadAnimationName);
            reloadAnimation.Stop();
        }

        UpdateAmmoText();
        OnAmmoChanged?.Invoke();
    }

    void HandleFired()
    {
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        BroadcastAmmoChanged();
        OnAmmoChanged?.Invoke();

        if (currentAmmo <= 0)
        {
            OnAmmoEmpty?.Invoke();
            StartReload();
        }
    }

    void Update()
    {
        if (reloadAction.WasPressedThisFrame() && !reloading && currentAmmo < maxAmmo)
            StartReload();

        if (reloading)
        {
            if (attackAction.WasPressedThisFrame() && currentAmmo > 0)
            {
                StopReload();
            }
            else
            {
                reloadTimer += Time.deltaTime;

                if (reloadAnimation != null && reloadClip != null)
                {
                    float animTime = (reloadTimer * reloadAnimationSpeed) % reloadClip.length;
                    reloadAnimation[reloadAnimationName].time = animTime;
                    reloadAnimation[reloadAnimationName].speed = 0f;
                    reloadAnimation.Sample();
                }

                if (reloadTimer >= reloadDuration)
                    FinishReload();
            }
        }

        UpdateReloadDip();
    }

    void StartReload()
    {
        if (reloading) return;
        if (currentAmmo >= maxAmmo) return;
        if (reloadAnimation == null || reloadClip == null) return;

        reloading = true;
        reloadTimer = 0f;
        weaponController.SetReloadBlocked(true);
        OnAnyReloadStart?.Invoke(currentAmmo);

        reloadAnimation.Play(reloadAnimationName);
        reloadAnimation[reloadAnimationName].time = 0f;
        reloadAnimation[reloadAnimationName].speed = 0f;
        reloadAnimation.Sample();
    }

    void StopReload()
    {
        if (!reloading) return;

        reloading = false;
        weaponController.SetReloadBlocked(false);

        if (reloadAnimation != null)
            reloadAnimation.Stop();

        StartReloadDip();
    }

    void FinishReload()
    {
        reloading = false;
        currentAmmo = maxAmmo;
        weaponController.SetReloadBlocked(false);
        BroadcastAmmoChanged();
        OnAmmoChanged?.Invoke();

        if (reloadAnimation != null)
            reloadAnimation.Stop();

        StartReloadDip();
    }

    void StartReloadDip()
    {
        reloadDipping = true;
        reloadDipReturning = false;
        reloadDipOffset = 0f;
    }

    void UpdateReloadDip()
    {
        if (dipTarget == null) return;

        if (reloadDipping)
        {
            reloadDipOffset = Mathf.MoveTowards(reloadDipOffset, reloadDipAmount, reloadDipSpeed * Time.deltaTime);
            dipTarget.localPosition = dipTargetStartPos + new Vector3(0f, reloadDipOffset, 0f);

            if (Mathf.Approximately(reloadDipOffset, reloadDipAmount))
            {
                reloadDipping = false;
                reloadDipReturning = true;
            }
        }

        if (reloadDipReturning)
        {
            reloadDipOffset = Mathf.MoveTowards(reloadDipOffset, 0f, reloadDipReturnSpeed * Time.deltaTime);
            dipTarget.localPosition = dipTargetStartPos + new Vector3(0f, reloadDipOffset, 0f);

            if (Mathf.Approximately(reloadDipOffset, 0f))
                reloadDipReturning = false;
        }
    }

    void UpdateAmmoText()
    {
        if (ammoText != null)
            ammoText.text = $"{currentAmmo}/{maxAmmo}";
    }

    void BroadcastAmmoChanged()
    {
        UpdateAmmoText();
        OnAnyAmmoChanged?.Invoke(currentAmmo, maxAmmo);
    }

    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
    public bool IsReloading() => reloading;
    public void SetAmmo(int ammo)
    {
        currentAmmo = Mathf.Clamp(ammo, 0, maxAmmo);
        BroadcastAmmoChanged();
        OnAmmoChanged?.Invoke();
    }
}