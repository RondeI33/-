using UnityEngine;

public class KillSlowMotion : MonoBehaviour
{
    [SerializeField, Range(0.01f, 1f)] private float lastKillSlomoScale = 0.05f;
    [SerializeField] private float lastKillSlomoDuration = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private float defaultFixedDeltaTime;
    private float timer;
    private float currentScale = 1f;
    private bool active;
    private bool wasDelegating;

    public static KillSlowMotion Instance;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        Instance = this;
    }

    public void Trigger(bool lastEnemy = false)
    {
        if (!lastEnemy) return;
        timer = Mathf.Max(timer, lastKillSlomoDuration);
        active = true;
    }

    public float GetTargetScale()
    {
        if (!active) return 1f;
        return timer > 0f ? lastKillSlomoScale : 1f;
    }

    public bool IsActive() => active;

    private void Update()
    {
        if (!active) return;

        timer -= Time.unscaledDeltaTime;

        bool slowMotionHandling = SlowMotion.Instance != null && SlowMotion.Instance.enabled;

        if (slowMotionHandling)
        {
            wasDelegating = true;
            if (timer <= 0f)
                active = false;
            return;
        }

        if (wasDelegating)
        {
            currentScale = Time.timeScale;
            wasDelegating = false;
        }

        float target = timer > 0f ? lastKillSlomoScale : 1f;
        currentScale = Mathf.MoveTowards(currentScale, target, Time.unscaledDeltaTime * transitionSpeed);
        Time.timeScale = currentScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * currentScale;

        if (timer <= 0f && Mathf.Approximately(currentScale, 1f))
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
            active = false;
        }
    }
}