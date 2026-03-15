using UnityEngine;

public class WeaponRecoilController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform recoilPivot;

    [Header("Recoil Shape")]
    [SerializeField] float recoilDistance = 0.08f;
    [SerializeField] float recoilRotation = 3f;
    [SerializeField] Vector3 recoilRotationAxis = new Vector3(-1f, 0f, 0f);
    [SerializeField] Vector3 recoilPushAxis = new Vector3(0f, 0f, -1f);

    [Header("Recoil Feel")]
    [SerializeField] float recoilSpeed = 20f;
    [SerializeField] float recoverySpeed = 6f;
    [SerializeField] float rotationDelay = 0.05f;

    private WeaponController weaponController;

    private Vector3 currentRecoil;
    private Vector3 targetRecoil;
    private Vector3 currentRotationRecoil;
    private Vector3 targetRotationRecoil;
    private Vector3 pendingRotationRecoil;
    private float rotationDelayTimer;

    private Vector3 pivotStartLocalPos;
    private Quaternion pivotStartLocalRot;

    void Awake()
    {
        weaponController = GetComponent<WeaponController>();
    }

    void OnEnable()
    {
        if (weaponController != null)
            weaponController.OnFired += HandleFired;
    }

    void OnDisable()
    {
        if (weaponController != null)
            weaponController.OnFired -= HandleFired;
    }

    void Start()
    {
        if (recoilPivot != null)
        {
            pivotStartLocalPos = recoilPivot.localPosition;
            pivotStartLocalRot = recoilPivot.localRotation;
        }
    }

    void HandleFired()
    {
        // Only kick if not already mid-kick — keeps auto-fire consistent
        if (targetRecoil.sqrMagnitude < 0.0001f)
            targetRecoil = recoilPushAxis.normalized * recoilDistance;

        pendingRotationRecoil = recoilRotationAxis * recoilRotation;
        rotationDelayTimer = rotationDelay;
    }

    void Update()
    {
        // Delayed rotation kick (same behaviour as old script)
        if (rotationDelayTimer > 0f)
        {
            rotationDelayTimer -= Time.deltaTime;
            if (rotationDelayTimer <= 0f)
                targetRotationRecoil = pendingRotationRecoil;
        }

        // Position spring
        float posSpeed = targetRecoil.sqrMagnitude > currentRecoil.sqrMagnitude
            ? recoilSpeed
            : recoverySpeed;
        currentRecoil = Vector3.Lerp(currentRecoil, targetRecoil, Time.deltaTime * posSpeed);

        // Rotation spring
        float rotSpeed = targetRotationRecoil.sqrMagnitude > currentRotationRecoil.sqrMagnitude
            ? recoilSpeed
            : recoverySpeed;
        currentRotationRecoil = Vector3.Lerp(currentRotationRecoil, targetRotationRecoil, Time.deltaTime * rotSpeed);

        // Clear targets once close enough (let recovery spring back to zero)
        if (targetRecoil != Vector3.zero && (currentRecoil - targetRecoil).sqrMagnitude < 0.000001f)
            targetRecoil = Vector3.zero;

        if (targetRotationRecoil != Vector3.zero && (currentRotationRecoil - targetRotationRecoil).sqrMagnitude < 0.000001f)
            targetRotationRecoil = Vector3.zero;

        // Apply to pivot
        if (recoilPivot != null)
        {
            recoilPivot.localPosition = pivotStartLocalPos + currentRecoil;
            recoilPivot.localRotation = pivotStartLocalRot * Quaternion.Euler(currentRotationRecoil);
        }
    }
}
