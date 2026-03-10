using UnityEngine;
using UnityEngine.InputSystem;

public class DoubleJump : MonoBehaviour
{
    [SerializeField] private float doubleJumpMultiplier = 0.85f;
    [SerializeField] private int maxExtraJumps = 1;
    [SerializeField] private Renderer[] indicatorRenderers;
    [SerializeField] private Color indicatorColor = Color.blue;
    [SerializeField] private Color disabledColor = Color.black;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private AudioSource jumpAudioSource;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private float jumpPitchMin = 0.9f;
    [SerializeField] private float jumpPitchMax = 1.1f;

    private FirstPersonController playerController;
    private PlayerInput playerInput;
    private CoinThrow coinThrow;
    private InputAction jumpAction;
    private Material[] indicatorMats;
    private int jumpsRemaining;
    private bool wasGrounded;
    private bool wasJumpPressed;
    private float fadeT = 0f;
    private float fadeDirection = 0f;

    private void Start()
    {
        playerController = GetComponentInParent<FirstPersonController>();
        playerInput = GetComponentInParent<PlayerInput>();
        coinThrow = GetComponentInParent<CoinThrow>();
        jumpAction = playerInput.actions["Jump"];
        wasGrounded = playerController.IsGrounded();
        jumpsRemaining = maxExtraJumps;
        indicatorMats = new Material[indicatorRenderers.Length];
        for (int i = 0; i < indicatorRenderers.Length; i++)
        {
            if (indicatorRenderers[i])
                indicatorMats[i] = indicatorRenderers[i].material;
        }
    }

    private void OnEnable()
    {
        jumpsRemaining = maxExtraJumps;

        if (playerController != null && playerController.IsGrounded())
        {
            fadeT = 0f;
            fadeDirection = 0f;
            if (indicatorMats != null)
            {
                for (int i = 0; i < indicatorMats.Length; i++)
                {
                    if (indicatorMats[i])
                        indicatorMats[i].SetColor("_BaseColor", disabledColor);
                }
            }
        }
        else
        {
            fadeT = 1f;
            fadeDirection = 0f;
            if (indicatorMats != null)
            {
                for (int i = 0; i < indicatorMats.Length; i++)
                {
                    if (indicatorMats[i])
                        indicatorMats[i].SetColor("_BaseColor", indicatorColor);
                }
            }
        }
    }

    private void Update()
    {
        bool grounded = playerController.IsGrounded();

        if (grounded && !wasGrounded)
        {
            jumpsRemaining = maxExtraJumps;
            fadeDirection = -1f;
        }

        if (!grounded && wasGrounded)
        {
            wasJumpPressed = jumpAction.IsPressed();
            if (jumpsRemaining > 0)
                fadeDirection = 1f;
        }

        if (!grounded && jumpAction.WasPressedThisFrame() && jumpsRemaining > 0)
        {
            if (!wasJumpPressed || !jumpAction.IsPressed())
            {
                bool buffed = coinThrow != null && coinThrow.IsBuffActive;
                float multiplier = buffed ? doubleJumpMultiplier * 2f : doubleJumpMultiplier;
                playerController.SetVerticalVelocity(playerController.GetJumpVelocity() * multiplier);
                jumpsRemaining--;
                PlaySound(jumpAudioSource, jumpSound);
                if (jumpsRemaining <= 0)
                    fadeDirection = -1f;
            }
        }

        if (!grounded)
            wasJumpPressed = false;

        wasGrounded = grounded;
        UpdateIndicator();
    }

    private void PlaySound(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.pitch = Random.Range(jumpPitchMin, jumpPitchMax);
        source.clip = clip;
        source.Play();
    }

    private void UpdateIndicator()
    {
        if (indicatorMats == null || indicatorMats.Length == 0) return;

        if (fadeDirection != 0f)
        {
            fadeT += fadeDirection * (Time.deltaTime / fadeDuration);
            fadeT = Mathf.Clamp01(fadeT);
            if (fadeT <= 0f || fadeT >= 1f)
                fadeDirection = 0f;
        }

        Color color = Color.Lerp(disabledColor, indicatorColor, fadeT);
        for (int i = 0; i < indicatorMats.Length; i++)
        {
            if (indicatorMats[i])
                indicatorMats[i].SetColor("_BaseColor", color);
        }
    }

    private void OnDestroy()
    {
        if (indicatorMats == null) return;
        for (int i = 0; i < indicatorMats.Length; i++)
        {
            if (indicatorMats[i])
                Destroy(indicatorMats[i]);
        }
    }
}