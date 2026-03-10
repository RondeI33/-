using UnityEngine;
public class RegenBuff : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private CoinThrow coinThrow;

    [SerializeField] private float passiveRegenRate = 3f;
    [SerializeField] private float coinBuffRegenMultiplier = 4f;

    private void Update()
    {
        if (playerHealth == null) return;
        float current = playerHealth.GetHealth();
        float max = playerHealth.GetMaxHealth();
        if (current >= max || current <= 0f) return;

        float rate = passiveRegenRate;
        if (coinThrow != null && coinThrow.IsBuffActive)
            rate *= coinBuffRegenMultiplier;

        float healAmount = rate * Time.deltaTime;
        if (playerHealth.IsRegenActive())
            healAmount += playerHealth.GetRegenRate() * 2f * Time.deltaTime;

        playerHealth.Heal(healAmount);
    }
}