using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip level2Music;
    [SerializeField][Range(0f, 1f)] private float volume = 0.5f;

    private AudioSource menuSource;
    private AudioSource gameSource;
    private AudioSource transitionSource;
    private bool gameStarted = false;
    private bool wasPaused = false;
    private bool waitingForTransition = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        menuSource = gameObject.AddComponent<AudioSource>();
        menuSource.clip = menuMusic;
        menuSource.loop = true;
        menuSource.volume = volume;
        menuSource.playOnAwake = false;

        gameSource = gameObject.AddComponent<AudioSource>();
        gameSource.clip = gameMusic;
        gameSource.loop = true;
        gameSource.volume = volume;
        gameSource.playOnAwake = false;

        transitionSource = gameObject.AddComponent<AudioSource>();
        transitionSource.loop = false;
        transitionSource.volume = volume;
        transitionSource.playOnAwake = false;

        menuSource.Play();
    }

    void Update()
    {
        if (waitingForTransition)
        {
            if (!transitionSource.isPlaying)
            {
                waitingForTransition = false;
                gameSource.clip = level2Music;
                gameSource.Play();
                wasPaused = false;
            }
            return;
        }

        if (!gameStarted) return;

        bool paused = GamePauser.IsPaused;

        if (paused && !wasPaused)
        {
            gameSource.Pause();
            menuSource.UnPause();
            if (!menuSource.isPlaying)
                menuSource.Play();
        }
        else if (!paused && wasPaused)
        {
            menuSource.Pause();
            gameSource.UnPause();
        }

        wasPaused = paused;
    }

    public void StartGameMusic()
    {
        gameStarted = true;
        wasPaused = false;
        menuSource.Pause();
        gameSource.Play();
    }

    public void PlayTransitionThenLevel2(AudioClip transitionClip)
    {
        gameSource.Stop();
        menuSource.Stop();
        transitionSource.clip = transitionClip;
        transitionSource.Play();
        waitingForTransition = true;
    }

    public void ReturnToMenu()
    {
        gameStarted = false;
        waitingForTransition = false;
        gameSource.Stop();
        menuSource.Stop();
        transitionSource.Stop();
        gameSource.clip = gameMusic;
        menuSource.Play();
    }
}