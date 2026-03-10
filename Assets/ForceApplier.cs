using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ForceApplier : MonoBehaviour
{
    [Header("KuleMocy ustawienia")]
    [SerializeField] private float mass = 1f;
    [SerializeField][Range(0f, 1f)] private float decelerationFactor = 0.066f;
    [SerializeField] private float maxVelocity = 2137f;

    private CharacterController characterController;
    private Vector3 velocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        ApplyDeceleration();
        MoveWithVelocity();
    }

    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        switch (mode)
        {
            case ForceMode.Force:
                velocity += (force / mass) * Time.fixedDeltaTime;
                break;

            case ForceMode.Impulse:
                velocity += force / mass;
                break;

            case ForceMode.VelocityChange:
                velocity += force;
                break;

            case ForceMode.Acceleration:
                velocity += force * Time.fixedDeltaTime;
                break;
        }

        if (velocity.magnitude > maxVelocity)
        {
            velocity = velocity.normalized * maxVelocity;
        }
    }

    private void ApplyDeceleration()
    {
        float decelerationThisFrame = 1f - Mathf.Pow(1f - decelerationFactor, Time.fixedDeltaTime * 60f);
        velocity *= (1f - decelerationThisFrame);

        if (velocity.magnitude < 0.01f)
        {
            velocity = Vector3.zero;
        }
    }

    private void MoveWithVelocity()
    {
        if (velocity.magnitude > 0.01f)
        {
            characterController.Move(velocity * Time.fixedDeltaTime);
        }
        else if (velocity.magnitude > 0.00f)
        {
            velocity = Vector3.zero;
        }
    }

    public Vector3 GetVelocity()
    {
        return velocity;
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }

    public void SetDecelerationFactor(float factor)
    {
        decelerationFactor = Mathf.Clamp01(factor);
    }
}