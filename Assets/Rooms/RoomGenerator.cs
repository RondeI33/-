using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;


public class RoomGenerator : MonoBehaviour
{
    [SerializeField] private int minRoomCount = 13;
    [SerializeField] private int maxRoomCount = 23;
    [SerializeField] private GameObject startRoomPrefab;
    [SerializeField] private GameObject tutorialRoomPrefab;
    [SerializeField] private GameObject[] roomPrefabs;
    [SerializeField] private GameObject[] treasureRoomPrefabs;
    [SerializeField] private GameObject endRoomPrefab;
    [SerializeField] private float boundsShrink = 0.5f;
    [SerializeField] private GameObject[] prefabsToCleanOnSwap;
    [SerializeField] private LoadingScreenController loadingScreen;
    [SerializeField] private int treasureRoomCount = 2;

    private RoomActivator roomActivator;
    private List<GameObject> spawnedRooms = new List<GameObject>();
    private List<Bounds> placedBounds = new List<Bounds>();
    private List<int> spawnedPrefabIndices = new List<int>();
    private List<bool> spawnedIsTreasure = new List<bool>();
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<Quaternion> spawnedRotations = new List<Quaternion>();
    private GameObject startRoom;
    private Bounds startBounds;
    private GameObject endRoom;
    private bool hasTutorialRoom = false;

    public IEnumerator Generate()
    {
        int roomCount = Random.Range(minRoomCount, maxRoomCount + 1);
        int maxFullRetries = 67;

        startRoom = Instantiate(startRoomPrefab, transform.position, transform.rotation);
        startBounds = GetCombinedBounds(startRoom);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Transform spawnPoint = startRoom.transform.Find("Start");
            if (spawnPoint != null)
            {
                CharacterController cc = playerObj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerObj.transform.position = spawnPoint.position;
                if (cc != null) cc.enabled = true;
            }
        }

        Transform startExit = startRoom.transform.Find("Exit");

        hasTutorialRoom = tutorialRoomPrefab != null;

        for (int retry = 0; retry < maxFullRetries; retry++)
        {
            ClearRooms();
            placedBounds.Add(startBounds);

            Transform currentExit = startExit;

            if (hasTutorialRoom)
            {
                GameObject tutRoom = Instantiate(tutorialRoomPrefab, Vector3.zero, Quaternion.identity);
                Transform tutEntrance = tutRoom.transform.Find("Entrence");

                Quaternion targetRot = Quaternion.LookRotation(-currentExit.forward, Vector3.up);
                Quaternion rotDiff = targetRot * Quaternion.Inverse(tutEntrance.rotation);
                tutRoom.transform.rotation = rotDiff * tutRoom.transform.rotation;

                Vector3 offset = currentExit.position - tutEntrance.position;
                tutRoom.transform.position += offset;

                spawnedRooms.Add(tutRoom);
                placedBounds.Add(GetCombinedBounds(tutRoom));
                spawnedPrefabIndices.Add(-1);
                spawnedIsTreasure.Add(false);
                spawnedPositions.Add(tutRoom.transform.position);
                spawnedRotations.Add(tutRoom.transform.rotation);

                currentExit = tutRoom.transform.Find("Exit");
            }

            int firstPrefabIndex = Random.Range(0, roomPrefabs.Length);
            GameObject firstRoom = Instantiate(roomPrefabs[firstPrefabIndex], Vector3.zero, Quaternion.identity);
            Transform firstEntrance = firstRoom.transform.Find("Entrence");

            Quaternion targetRotation = Quaternion.LookRotation(-currentExit.forward, Vector3.up);
            Quaternion rotationDifference = targetRotation * Quaternion.Inverse(firstEntrance.rotation);
            firstRoom.transform.rotation = rotationDifference * firstRoom.transform.rotation;

            Vector3 posOffset = currentExit.position - firstEntrance.position;
            firstRoom.transform.position += posOffset;

            spawnedRooms.Add(firstRoom);
            placedBounds.Add(GetCombinedBounds(firstRoom));
            spawnedPrefabIndices.Add(firstPrefabIndex);
            spawnedIsTreasure.Add(false);
            spawnedPositions.Add(firstRoom.transform.position);
            spawnedRotations.Add(firstRoom.transform.rotation);

            int normalTarget = roomCount + (hasTutorialRoom ? 1 : 0);

            yield return StartCoroutine(BuildNormalChainCoroutine(normalTarget));

            if (!buildChainResult)
                continue;

            yield return StartCoroutine(BuildTreasureRoomsCoroutine());

            if (buildChainResult)
            {
                PlaceEndRoom();
                BuildAllNavMeshes();
                InitRoomActivator();

                if (loadingScreen != null)
                    loadingScreen.StartFadeOut();

                yield break;
            }
        }

        PlaceEndRoom();
        BuildAllNavMeshes();
        InitRoomActivator();

        if (loadingScreen != null)
            loadingScreen.StartFadeOut();
    }

    private void BuildAllNavMeshes()
    {
        for (int i = 0; i < spawnedRooms.Count; i++)
        {
            if (spawnedRooms[i] == null) continue;
            NavMeshSurface surface = spawnedRooms[i].GetComponentInChildren<NavMeshSurface>();
            if (surface != null)
                surface.BuildNavMesh();
        }

        if (endRoom != null)
        {
            NavMeshSurface surface = endRoom.GetComponentInChildren<NavMeshSurface>();
            if (surface != null)
                surface.BuildNavMesh();
        }
    }

    private void InitRoomActivator()
    {
        if (roomActivator == null)
            roomActivator = gameObject.AddComponent<RoomActivator>();
        roomActivator.Init(spawnedRooms, startRoom, endRoom);
    }

    private bool buildChainResult;

    private IEnumerator BuildNormalChainCoroutine(int roomCount)
    {
        int minKeep = hasTutorialRoom ? 2 : 1;
        buildChainResult = false;

        Dictionary<int, HashSet<int>> failedPrefabsPerStep = new Dictionary<int, HashSet<int>>();
        int totalBacktracks = 0;
        int maxTotalBacktracks = roomCount * 10;

        while (spawnedRooms.Count < roomCount)
        {
            int i = spawnedRooms.Count;
            Transform previousExit = spawnedRooms[spawnedRooms.Count - 1].transform.Find("Exit");

            if (!failedPrefabsPerStep.ContainsKey(i))
                failedPrefabsPerStep[i] = new HashSet<int>();

            HashSet<int> failedHere = failedPrefabsPerStep[i];

            if (failedHere.Count >= roomPrefabs.Length)
            {
                if (spawnedRooms.Count <= minKeep)
                {
                    buildChainResult = false;
                    yield break;
                }

                failedHere.Clear();

                int last = spawnedRooms.Count - 1;

                if (!failedPrefabsPerStep.ContainsKey(last))
                    failedPrefabsPerStep[last] = new HashSet<int>();
                failedPrefabsPerStep[last].Add(spawnedPrefabIndices[last]);

                Destroy(spawnedRooms[last]);
                spawnedRooms.RemoveAt(last);
                placedBounds.RemoveAt(last + 1);
                spawnedPrefabIndices.RemoveAt(last);
                spawnedIsTreasure.RemoveAt(last);
                spawnedPositions.RemoveAt(last);
                spawnedRotations.RemoveAt(last);

                totalBacktracks++;
                if (totalBacktracks > maxTotalBacktracks)
                {
                    buildChainResult = false;
                    yield break;
                }

                yield return null;
                continue;
            }

            List<int> candidates = new List<int>();
            for (int p = 0; p < roomPrefabs.Length; p++)
            {
                if (!failedHere.Contains(p))
                    candidates.Add(p);
            }

            for (int s = candidates.Count - 1; s > 0; s--)
            {
                int r = Random.Range(0, s + 1);
                int tmp = candidates[s];
                candidates[s] = candidates[r];
                candidates[r] = tmp;
            }

            bool placed = false;
            foreach (int prefabIndex in candidates)
            {
                GameObject newRoom = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                Transform newEntrance = newRoom.transform.Find("Entrence");

                Quaternion target = Quaternion.LookRotation(-previousExit.forward, Vector3.up);
                Quaternion rotDiff = target * Quaternion.Inverse(newEntrance.rotation);
                newRoom.transform.rotation = rotDiff * newRoom.transform.rotation;

                Vector3 posOffset = previousExit.position - newEntrance.position;
                newRoom.transform.position += posOffset;

                Bounds newBounds = GetCombinedBounds(newRoom);

                if (!OverlapsAny(newBounds))
                {
                    spawnedRooms.Add(newRoom);
                    placedBounds.Add(newBounds);
                    spawnedPrefabIndices.Add(prefabIndex);
                    spawnedIsTreasure.Add(false);
                    spawnedPositions.Add(newRoom.transform.position);
                    spawnedRotations.Add(newRoom.transform.rotation);
                    placed = true;
                    break;
                }

                failedHere.Add(prefabIndex);
                Destroy(newRoom);
            }

            if (!placed)
            {
                yield return null;
                continue;
            }

            yield return null;
        }

        buildChainResult = true;
    }

    private IEnumerator BuildTreasureRoomsCoroutine()
    {
        int placed = 0;
        int maxSpacers = 10;

        while (placed < treasureRoomCount)
        {
            bool treasurePlaced = false;

            Transform previousExit = spawnedRooms[spawnedRooms.Count - 1].transform.Find("Exit");

            List<int> candidates = new List<int>();
            for (int p = 0; p < treasureRoomPrefabs.Length; p++)
                candidates.Add(p);

            for (int s = candidates.Count - 1; s > 0; s--)
            {
                int r = Random.Range(0, s + 1);
                int tmp = candidates[s];
                candidates[s] = candidates[r];
                candidates[r] = tmp;
            }

            foreach (int prefabIndex in candidates)
            {
                GameObject newRoom = Instantiate(treasureRoomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                Transform newEntrance = newRoom.transform.Find("Entrence");

                Quaternion target = Quaternion.LookRotation(-previousExit.forward, Vector3.up);
                Quaternion rotDiff = target * Quaternion.Inverse(newEntrance.rotation);
                newRoom.transform.rotation = rotDiff * newRoom.transform.rotation;

                Vector3 posOffset = previousExit.position - newEntrance.position;
                newRoom.transform.position += posOffset;

                Bounds newBounds = GetCombinedBounds(newRoom);

                if (!OverlapsAny(newBounds))
                {
                    spawnedRooms.Add(newRoom);
                    placedBounds.Add(newBounds);
                    spawnedPrefabIndices.Add(prefabIndex);
                    spawnedIsTreasure.Add(true);
                    spawnedPositions.Add(newRoom.transform.position);
                    spawnedRotations.Add(newRoom.transform.rotation);
                    treasurePlaced = true;
                    placed++;
                    break;
                }

                Destroy(newRoom);
            }

            if (!treasurePlaced)
            {
                bool spacerPlaced = false;
                int spacerAttempts = 0;

                while (!spacerPlaced && spacerAttempts < maxSpacers)
                {
                    spacerAttempts++;
                    previousExit = spawnedRooms[spawnedRooms.Count - 1].transform.Find("Exit");

                    List<int> spacerCandidates = new List<int>();
                    for (int p = 0; p < roomPrefabs.Length; p++)
                        spacerCandidates.Add(p);

                    for (int s = spacerCandidates.Count - 1; s > 0; s--)
                    {
                        int r = Random.Range(0, s + 1);
                        int tmp = spacerCandidates[s];
                        spacerCandidates[s] = spacerCandidates[r];
                        spacerCandidates[r] = tmp;
                    }

                    foreach (int prefabIndex in spacerCandidates)
                    {
                        GameObject spacer = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                        Transform spacerEntrance = spacer.transform.Find("Entrence");

                        Quaternion target = Quaternion.LookRotation(-previousExit.forward, Vector3.up);
                        Quaternion rotDiff = target * Quaternion.Inverse(spacerEntrance.rotation);
                        spacer.transform.rotation = rotDiff * spacer.transform.rotation;

                        Vector3 posOffset = previousExit.position - spacerEntrance.position;
                        spacer.transform.position += posOffset;

                        Bounds spacerBounds = GetCombinedBounds(spacer);

                        if (!OverlapsAny(spacerBounds))
                        {
                            spawnedRooms.Add(spacer);
                            placedBounds.Add(spacerBounds);
                            spawnedPrefabIndices.Add(prefabIndex);
                            spawnedIsTreasure.Add(false);
                            spawnedPositions.Add(spacer.transform.position);
                            spawnedRotations.Add(spacer.transform.rotation);
                            spacerPlaced = true;
                            break;
                        }

                        Destroy(spacer);
                    }

                    yield return null;
                }

                if (!spacerPlaced)
                {
                    buildChainResult = true;
                    yield break;
                }
            }

            yield return null;
        }

        buildChainResult = true;
    }

    private void PlaceEndRoom()
    {
        if (spawnedRooms.Count == 0 || endRoomPrefab == null)
            return;

        Transform lastExit = spawnedRooms[spawnedRooms.Count - 1].transform.Find("Exit");
        endRoom = Instantiate(endRoomPrefab, Vector3.zero, Quaternion.identity);
        Transform endEntrance = endRoom.transform.Find("Entrence");

        Quaternion target = Quaternion.LookRotation(-lastExit.forward, Vector3.up);
        Quaternion rotDiff = target * Quaternion.Inverse(endEntrance.rotation);
        endRoom.transform.rotation = rotDiff * endRoom.transform.rotation;

        Vector3 posOffset = lastExit.position - endEntrance.position;
        endRoom.transform.position += posOffset;

        Bounds endBounds = GetCombinedBounds(endRoom);
        if (OverlapsAny(endBounds))
        {
            Destroy(endRoom);
            endRoom = null;
        }
    }

    private Bounds GetCombinedBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(room.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private bool OverlapsAny(Bounds newBounds)
    {
        Bounds shrunk = new Bounds(newBounds.center, newBounds.size - Vector3.one * boundsShrink * 2);

        for (int i = 0; i < placedBounds.Count; i++)
        {
            Bounds existing = new Bounds(placedBounds[i].center, placedBounds[i].size - Vector3.one * boundsShrink * 2);
            if (shrunk.Intersects(existing))
                return true;
        }

        return false;
    }

    public void ClearRooms()
    {
        foreach (GameObject room in spawnedRooms)
        {
            if (room != null)
                Destroy(room);
        }
        if (endRoom != null)
            Destroy(endRoom);

        spawnedRooms.Clear();
        placedBounds.Clear();
        spawnedPrefabIndices.Clear();
        spawnedIsTreasure.Clear();
        spawnedPositions.Clear();
        spawnedRotations.Clear();
        endRoom = null;
    }
}