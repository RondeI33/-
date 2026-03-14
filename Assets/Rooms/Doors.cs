using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class Doors : MonoBehaviour
{
    private GameObject doorPrefab;
    private GameObject animatedDoorPrefab;
    private GameObject triggerPrefab;
    private GameObject endPrefab;
    private string animationClipName = "open";

    private List<Transform> entrancePoints = new List<Transform>();
    private HashSet<Transform> deadEndPointSet = new HashSet<Transform>();
    private List<Collider> entranceTriggers = new List<Collider>();
    private List<GameObject> doors = new List<GameObject>();
    private List<Animation> animatedDoors = new List<Animation>();
    private List<IEnemy> enemies = new List<IEnemy>();
    private NavMeshSurface navMeshSurface;
    private bool playerInRoom;
    private bool roomCleared;
    private bool enemiesSpawned;
    private int triggerOverlapCount;
    private Transform player;
    private Bounds roomBounds;
    private bool boundsInitialized;
    private float boundsCheckTimer;
    private const float BoundsCheckInterval = 0.5f;
    private Collider enteredTrigger;
    private ExitArrowIndicator exitArrow;
    private bool initialized;

    public void Init(HashSet<Transform> deadEnds)
    {
        if (initialized) return;
        initialized = true;

        deadEndPointSet = deadEnds;

        doorPrefab = Resources.Load<GameObject>("Door");
        animatedDoorPrefab = Resources.Load<GameObject>("AnimatedDoor");
        triggerPrefab = Resources.Load<GameObject>("Trigger");
        endPrefab = Resources.Load<GameObject>("End");

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            exitArrow = playerObj.GetComponent<ExitArrowIndicator>();
        }

        navMeshSurface = GetComponentInChildren<NavMeshSurface>();
        FindEntrancePoints();

        IEnemy[] found = GetComponentsInChildren<IEnemy>(true);
        for (int i = 0; i < found.Length; i++)
        {
            ((MonoBehaviour)found[i]).gameObject.SetActive(false);
            enemies.Add(found[i]);
            found[i].SetDoors(this);
        }

        InitRoomBounds();
        SpawnTriggers();
        SpawnDoors();

        for (int i = 0; i < doors.Count; i++)
            if (doors[i] != null)
                doors[i].SetActive(false);

        for (int i = 0; i < animatedDoors.Count; i++)
        {
            if (animatedDoors[i] == null) continue;
            if (animatedDoors[i][animationClipName] == null) continue;
            AnimationState state = animatedDoors[i][animationClipName];
            state.speed = 0f;
            state.time = 0f;
            animatedDoors[i].Play(animationClipName);
            animatedDoors[i].Sample();
            animatedDoors[i].Stop();
        }
    }

    private void Start()
    {
        if (!initialized)
            Init(new HashSet<Transform>());
    }

    private void FindEntrancePoints()
    {
        Transform root = transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == "Entrence" || child.name == "Exit")
                entrancePoints.Add(child);
        }
    }

    private void SpawnTriggers()
    {
        if (triggerPrefab == null) return;
        for (int i = 0; i < entrancePoints.Count; i++)
        {
            if (entrancePoints[i] == null) continue;
            GameObject triggerObj = Instantiate(triggerPrefab, entrancePoints[i].position, entrancePoints[i].rotation, transform);
            Collider col = triggerObj.GetComponent<Collider>();
            if (col == null) col = triggerObj.GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                entranceTriggers.Add(col);
            }
        }
    }

    private void SpawnDoors()
    {
        for (int i = 0; i < entrancePoints.Count; i++)
        {
            if (entrancePoints[i] == null) continue;

            Vector3 pos = entrancePoints[i].position;
            Quaternion rot = entrancePoints[i].rotation;

            if (doorPrefab != null)
            {
                GameObject door = Instantiate(doorPrefab, pos, rot, transform);
                doors.Add(door);
            }

            if (deadEndPointSet.Contains(entrancePoints[i]))
            {
                if (endPrefab != null)
                    Instantiate(endPrefab, pos, rot, transform);
            }
            else
            {
                if (animatedDoorPrefab != null)
                {
                    GameObject animDoor = Instantiate(animatedDoorPrefab, pos, rot, transform);
                    Animation anim = animDoor.GetComponent<Animation>();
                    if (anim == null) anim = animDoor.GetComponentInChildren<Animation>();
                    if (anim != null) animatedDoors.Add(anim);
                }
            }
        }
    }

    private void InitRoomBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        roomBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            roomBounds.Encapsulate(renderers[i].bounds);
        roomBounds.Expand(4f);
        boundsInitialized = true;
    }

    private void Update()
    {
        if (!playerInRoom || roomCleared || !boundsInitialized) return;
        boundsCheckTimer -= Time.deltaTime;
        if (boundsCheckTimer > 0f) return;
        boundsCheckTimer = BoundsCheckInterval;

        bool anyInside = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || (enemies[i] as MonoBehaviour) == null) continue;
            if (roomBounds.Contains(((MonoBehaviour)enemies[i]).transform.position))
            {
                anyInside = true;
                break;
            }
        }

        if (!anyInside) CheckRoomCleared();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.gameObject.layer != LayerMask.NameToLayer("Player")) return;

        triggerOverlapCount++;

        RoomActivator roomActivator = FindFirstObjectByType<RoomActivator>();
        if (roomActivator != null)
            roomActivator.OnPlayerEnteredRoom(gameObject);

        if (exitArrow != null && exitArrow.GetActiveDoors() != null && exitArrow.GetActiveDoors() != this)
            exitArrow.Hide();

        if (entranceTriggers.Count > 0)
            enteredTrigger = FindClosestTrigger(player.position);

        if (roomCleared) return;

        if (!enemiesSpawned)
        {
            enemiesSpawned = true;

            if (enemies.Count == 0)
            {
                roomCleared = true;
                ShowExitArrow();
                return;
            }

            playerInRoom = true;
            SetDoors(true);
            RebakeNavMesh();

            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null)
                    ((MonoBehaviour)enemies[i]).gameObject.SetActive(true);

            StartCoroutine(DelayedEnemyActivation());
        }
        else if (!roomCleared)
        {
            playerInRoom = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.gameObject.layer != LayerMask.NameToLayer("Player")) return;

        triggerOverlapCount--;
        if (triggerOverlapCount < 0) triggerOverlapCount = 0;

        if (triggerOverlapCount == 0)
            playerInRoom = false;
    }

    private IEnumerator DelayedEnemyActivation()
    {
        yield return null;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
            {
                enemies[i].InitAgent();
                enemies[i].Activate();
            }
        }
    }

    private Collider FindClosestTrigger(Vector3 position)
    {
        Collider closest = null;
        float closestDist = float.MaxValue;
        for (int i = 0; i < entranceTriggers.Count; i++)
        {
            if (entranceTriggers[i] == null) continue;
            float dist = Vector3.Distance(position, entranceTriggers[i].transform.position);
            if (dist < closestDist) { closestDist = dist; closest = entranceTriggers[i]; }
        }
        return closest;
    }

    public void OnEnemyDied(IEnemy enemy)
    {
        enemies.Remove(enemy);
        CheckRoomCleared();
    }

    public bool IsLastEnemy()
    {
        int alive = 0;
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && (enemies[i] as MonoBehaviour) != null)
                alive++;
        return alive <= 1;
    }

    private void CheckRoomCleared()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i] == null || (enemies[i] as MonoBehaviour) == null)
                enemies.RemoveAt(i);

        if (enemies.Count > 0) return;

        roomCleared = true;
        playerInRoom = false;
        SetDoors(false);
        RebakeNavMesh();
        ShowExitArrow();
    }

    private void ShowExitArrow()
    {
        if (exitArrow == null || entranceTriggers.Count == 0) return;

        Collider farthest = null;
        float farthestDist = -1f;
        for (int i = 0; i < entranceTriggers.Count; i++)
        {
            if (entranceTriggers[i] == null) continue;
            if (entranceTriggers[i] == enteredTrigger) continue;
            float dist = enteredTrigger != null
                ? Vector3.Distance(enteredTrigger.transform.position, entranceTriggers[i].transform.position)
                : 0f;
            if (dist > farthestDist) { farthestDist = dist; farthest = entranceTriggers[i]; }
        }

        Transform exitTarget = farthest != null ? farthest.transform : entranceTriggers[0].transform;
        if (exitTarget != null) exitArrow.Show(exitTarget, this);
    }

    private void SetDoors(bool active)
    {
        for (int i = 0; i < doors.Count; i++)
            if (doors[i] != null) doors[i].SetActive(active);

        for (int i = 0; i < animatedDoors.Count; i++)
        {
            if (animatedDoors[i] == null) continue;
            if (animatedDoors[i][animationClipName] == null) continue;
            AnimationState state = animatedDoors[i][animationClipName];
            if (active) { state.speed = 1f; state.time = 0f; }
            else { state.speed = -1f; state.time = state.length; }
            animatedDoors[i].Play(animationClipName);
        }
    }

    public void RefreshDoorState()
    {
        if (roomCleared)
        {
            for (int i = 0; i < doors.Count; i++)
                if (doors[i] != null) doors[i].SetActive(false);

            for (int i = 0; i < animatedDoors.Count; i++)
            {
                if (animatedDoors[i] == null) continue;
                if (animatedDoors[i][animationClipName] == null) continue;
                AnimationState state = animatedDoors[i][animationClipName];
                state.speed = 0f; state.time = 0f;
                animatedDoors[i].Play(animationClipName);
                animatedDoors[i].Sample();
                animatedDoors[i].Stop();
            }
        }
        else if (playerInRoom)
        {
            for (int i = 0; i < doors.Count; i++)
                if (doors[i] != null) doors[i].SetActive(true);

            for (int i = 0; i < animatedDoors.Count; i++)
            {
                if (animatedDoors[i] == null) continue;
                if (animatedDoors[i][animationClipName] == null) continue;
                AnimationState state = animatedDoors[i][animationClipName];
                state.speed = 0f; state.time = state.length;
                animatedDoors[i].Play(animationClipName);
                animatedDoors[i].Sample();
                animatedDoors[i].Stop();
            }
        }
    }

    private void RebakeNavMesh()
    {
        if (navMeshSurface == null) return;
        Transform root = navMeshSurface.transform;
        List<GameObject> wasDisabled = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (!child.activeSelf) { wasDisabled.Add(child); child.SetActive(true); }
        }
        navMeshSurface.BuildNavMesh();
        for (int i = 0; i < wasDisabled.Count; i++)
            wasDisabled[i].SetActive(false);
    }
}