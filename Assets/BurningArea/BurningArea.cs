using UnityEngine;

public class BurningArea : MonoBehaviour
{
    private BoxCollider boxCollider;
    private bool playerInside = false;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    private void Update()
    {
        if (boxCollider == null) return;

        Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
        Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale);

        Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);

        bool foundPlayer = false;
        foreach (Collider col in hits)
        {
            if (col.gameObject.layer != LayerMask.NameToLayer("Player")) continue;
            PlayerHealth health = col.GetComponent<PlayerHealth>();
            if (health == null) continue;

            foundPlayer = true;
            if (!playerInside)
            {
                playerInside = true;
                health.EnterFire();
            }
            break;
        }

        if (!foundPlayer && playerInside)
        {
            playerInside = false;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
                if (health != null)
                    health.ExitFire();
            }
        }
    }
}