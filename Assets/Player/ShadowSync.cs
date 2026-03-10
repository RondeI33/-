using UnityEngine;

public class ShadowSync : MonoBehaviour
{
    [SerializeField] private GameObject source;

    private WeaponSway sourceSway;
    private WeaponSway shadowSway;
    private WeaponRecoil sourceRecoil;
    private WeaponRecoil shadowRecoil;

    private void Start()
    {
        sourceSway = source.GetComponent<WeaponSway>();
        shadowSway = GetComponent<WeaponSway>();
        sourceRecoil = source.GetComponent<WeaponRecoil>();
        shadowRecoil = GetComponent<WeaponRecoil>();
    }

    private void Update()
    {
        if (sourceSway && shadowSway)
        {
            shadowSway.tiltAmount = sourceSway.tiltAmount;
            shadowSway.smoothSpeed = sourceSway.smoothSpeed;
            shadowSway.bobSpeed = sourceSway.bobSpeed;
            shadowSway.bobAmount = sourceSway.bobAmount;
            shadowSway.jumpBounceAmount = sourceSway.jumpBounceAmount;
            shadowSway.landBounceAmount = sourceSway.landBounceAmount;
            shadowSway.bounceSpeed = sourceSway.bounceSpeed;
            shadowSway.crouchOffset = sourceSway.crouchOffset;
            shadowSway.breathSpeed = sourceSway.breathSpeed;
            shadowSway.breathAmount = sourceSway.breathAmount;
        }
        if (sourceRecoil && shadowRecoil)
        {
            shadowRecoil.recoilDistance = sourceRecoil.recoilDistance;
            shadowRecoil.recoilSpeed = sourceRecoil.recoilSpeed;
            shadowRecoil.recoverySpeed = sourceRecoil.recoverySpeed;
            shadowRecoil.fullAuto = sourceRecoil.fullAuto;
            shadowRecoil.fireRate = sourceRecoil.fireRate;
            shadowRecoil.useCooldown = sourceRecoil.useCooldown;
            shadowRecoil.cooldownTime = sourceRecoil.cooldownTime;
        }
    }
}