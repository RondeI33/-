using UnityEngine;
public class RzezbaProjectile : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float minDamage = 15f;
    [SerializeField] private float maxDamage = 25f;
    [SerializeField] private float minEnemyDamage = 10f;
    [SerializeField] private float maxEnemyDamage = 20f;
    [SerializeField] private float knockbackForce = 25f;
    [SerializeField] private float enemyKnockbackForce = 15f;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float lifetime = 10f;
    [Header("Sound Effects")]
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = 1f;
    private bool exploded;
    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (exploded) return;
        exploded = true;
        Explode();
    }
    private void Explode()
    {
        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        System.Collections.Generic.HashSet<Transform> alreadyHit = new System.Collections.Generic.HashSet<Transform>();
        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            PlayerHealth ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && alreadyHit.Add(ph.transform))
            {
                float dist = Vector3.Distance(transform.position, ph.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                Vector3 dir = (ph.transform.position - transform.position).normalized;
                dir.y = Mathf.Max(dir.y, 0.3f);
                dir.Normalize();
                ph.TakeDamage(Mathf.Lerp(minDamage, maxDamage, falloff));
                ForceApplier fa = ph.GetComponent<ForceApplier>();
                if (fa != null)
                    fa.AddForce(dir * knockbackForce * falloff, ForceMode.Impulse);
            }
            EnemyForceApplier efa = hit.GetComponentInParent<EnemyForceApplier>();
            if (efa != null && alreadyHit.Add(efa.transform))
            {
                float dist = Vector3.Distance(transform.position, efa.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                Vector3 dir = (efa.transform.position - transform.position).normalized;
                dir.y = 0f;
                dir.Normalize();
                efa.AddForce(dir * enemyKnockbackForce * falloff, ForceMode.Impulse);
                IEnemy enemy = efa.GetComponentInParent<IEnemy>();
                if (enemy != null)
                    enemy.TakeDamage(Mathf.Lerp(minEnemyDamage, maxEnemyDamage, falloff));
            }
        }
        Destroy(gameObject);
    }
}