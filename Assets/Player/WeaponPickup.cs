using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private string side = "Left";
    [SerializeField] private string weaponName = "Revolver";
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float dropForce = 3f;
    [SerializeField] private float dropUpForce = 6.7f;
    private Camera mainCamera;
    private PlayerInput playerInput;
    private Transform playerRoot;
    private TextMeshProUGUI pickupText;
    private bool isLookedAt;
    void Start()
    {
        mainCamera = Camera.main;
        playerInput = FindFirstObjectByType<PlayerInput>();
        playerRoot = playerInput.transform;
        Transform pickupTextTransform = playerRoot.Find("HudUnMovable/PickupText");
        if (pickupTextTransform != null)
        {
            pickupText = pickupTextTransform.GetComponent<TextMeshProUGUI>();
            pickupText.gameObject.SetActive(false);
        }
    }
    void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }
        if (playerInput == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, pickupRange) && hit.collider.gameObject == gameObject)
        {
            if (!isLookedAt)
            {
                isLookedAt = true;
                if (pickupText != null)
                    pickupText.gameObject.SetActive(true);
            }
            if (playerInput.actions["Interact"].WasPressedThisFrame())
                Swap();
        }
        else
        {
            if (isLookedAt)
            {
                isLookedAt = false;
                if (pickupText != null)
                    pickupText.gameObject.SetActive(false);
            }
        }
    }
    private void SpawnDrop(Transform weaponSide)
    {
        foreach (Transform child in weaponSide)
        {
            if (!child.gameObject.activeSelf) continue;
            WeaponRecoil recoil = child.GetComponentInChildren<WeaponRecoil>();
            if (recoil == null || recoil.dropPrefab == null) break;
            GameObject dropped = Instantiate(recoil.dropPrefab, mainCamera.transform.position + mainCamera.transform.forward * 0.5f, Random.rotation);
            Rigidbody rb = dropped.GetComponent<Rigidbody>();
            if (rb != null)
            {
                CharacterController cc = playerRoot.GetComponent<CharacterController>();
                Vector3 playerVelocity = cc != null ? cc.velocity : Vector3.zero;
                rb.AddForce((mainCamera.transform.forward * dropForce + Vector3.up * dropUpForce) + playerVelocity, ForceMode.Impulse);
            }
            break;
        }
    }
    private void SwapNode(Transform parent)
    {
        Transform target = parent.Find(weaponName);
        if (target == null) return;
        foreach (Transform child in parent)
        {
            if (child == target)
                child.gameObject.SetActive(true);
            else if (child.name != "Panel")
                child.gameObject.SetActive(false);
        }
    }
    private void Swap()
    {
        Transform weaponSide = playerRoot.Find("Camera/" + side);
        bool isActiveSide = false;
        if (weaponSide != null)
        {
            isActiveSide = weaponSide.gameObject.activeSelf;
            SpawnDrop(weaponSide);
            SwapNode(weaponSide);
        }
        Transform inventorySide = playerRoot.Find("HudUnMovable/InventoryPanel/" + side);
        if (inventorySide != null)
            SwapNode(inventorySide);
        CoinThrow coinThrow = playerInput.GetComponent<CoinThrow>();
        if (coinThrow != null)
            coinThrow.RefreshActiveWeapon(isActiveSide);
        if (pickupText != null)
            pickupText.gameObject.SetActive(false);
        Destroy(gameObject);
    }
}