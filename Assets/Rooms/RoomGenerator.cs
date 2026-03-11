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
    [SerializeField] private GameObject[] prefabsToCleanOnSwap;
    [SerializeField] private LoadingScreenController loadingScreen;
    [SerializeField] private int treasureRoomCount = 2;
    [SerializeField] private float boundsGridCellSize = 5f;
    [SerializeField] private int overlapWindow = 2;
    [SerializeField] private int maxBranchDepth = 3;

    private RoomActivator roomActivator;
    private List<GameObject> spawnedRooms = new List<GameObject>();
    private List<List<Bounds>> placedMultiBounds = new List<List<Bounds>>();
    private List<int> spawnedPrefabIndices = new List<int>();
    private List<bool> spawnedIsTreasure = new List<bool>();
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<Quaternion> spawnedRotations = new List<Quaternion>();
    private List<Transform> spawnedUsedEntry = new List<Transform>();
    private List<List<GameObject>> branchRoomsPerMain = new List<List<GameObject>>();
    private GameObject startRoom;
    private List<Bounds> startMultiBounds;
    private GameObject endRoom;
    private bool hasTutorialRoom = false;

    private List<Transform> GetConnectionPoints(GameObject room, bool mainOnly)
    {
        List<Transform> points = new List<Transform>();
        Transform root = room.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != "Entrence" && child.name != "Exit") continue;

            bool isBranch = child.GetComponent<BranchPoint>() != null;
            if (mainOnly && isBranch) continue;

            points.Add(child);
        }
        return points;
    }

    private List<Transform> GetAllConnectionPoints(GameObject room)
    {
        return GetConnectionPoints(room, false);
    }

    private List<Transform> GetMainConnectionPoints(GameObject room)
    {
        return GetConnectionPoints(room, true);
    }

    private List<Transform> GetBranchOnlyPoints(GameObject room, Transform usedEntry)
    {
        List<Transform> points = new List<Transform>();
        Transform root = room.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != "Entrence" && child.name != "Exit") continue;
            if (child == usedEntry) continue;
            if (child.GetComponent<BranchPoint>() != null)
                points.Add(child);
        }
        return points;
    }

    private List<Transform> GetUnusedNonBranchPoints(GameObject room, Transform usedEntry)
    {
        List<Transform> points = new List<Transform>();
        Transform root = room.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != "Entrence" && child.name != "Exit") continue;
            if (child == usedEntry) continue;
            if (child.GetComponent<BranchPoint>() == null)
                points.Add(child);
        }
        return points;
    }

    private Transform GetExitPoint(GameObject room, Transform usedEntry)
    {
        List<Transform> points = GetMainConnectionPoints(room);
        Transform farthest = null;
        float farthestDist = -1f;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == usedEntry) continue;
            float dist = usedEntry != null
                ? Vector3.Distance(usedEntry.position, points[i].position)
                : 0f;
            if (dist > farthestDist)
            {
                farthestDist = dist;
                farthest = points[i];
            }
        }
        if (farthest == null)
        {
            List<Transform> allPoints = GetAllConnectionPoints(room);
            for (int i = 0; i < allPoints.Count; i++)
            {
                if (allPoints[i] == usedEntry) continue;
                float dist = usedEntry != null
                    ? Vector3.Distance(usedEntry.position, allPoints[i].position)
                    : 0f;
                if (dist > farthestDist)
                {
                    farthestDist = dist;
                    farthest = allPoints[i];
                }
            }
        }
        if (farthest == null && GetAllConnectionPoints(room).Count > 0)
            farthest = GetAllConnectionPoints(room)[0];
        return farthest;
    }

    private void AlignRoom(GameObject room, Transform roomEntry, Transform targetExit)
    {
        Quaternion targetRot = Quaternion.LookRotation(-targetExit.forward, Vector3.up);
        Quaternion rotDiff = targetRot * Quaternion.Inverse(roomEntry.rotation);
        room.transform.rotation = rotDiff * room.transform.rotation;

        Vector3 offset = targetExit.position - roomEntry.position;
        room.transform.position += offset;
    }

    public IEnumerator Generate()
    {
        int roomCount = Random.Range(minRoomCount, maxRoomCount + 1);
        int maxFullRetries = 67;

        startRoom = Instantiate(startRoomPrefab, transform.position, transform.rotation);
        startMultiBounds = GetMultiBounds(startRoom);

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
            placedMultiBounds.Add(startMultiBounds);

            Transform currentExit = startExit;

            if (hasTutorialRoom)
            {
                GameObject tutRoom = Instantiate(tutorialRoomPrefab, Vector3.zero, Quaternion.identity);
                Transform tutEntrance = tutRoom.transform.Find("Entrence");

                AlignRoom(tutRoom, tutEntrance, currentExit);

                placedMultiBounds.Add(GetMultiBounds(tutRoom));
                currentExit = GetExitPoint(tutRoom, tutEntrance);
                tutRoom.SetActive(false);

                spawnedRooms.Add(tutRoom);
                spawnedPrefabIndices.Add(-1);
                spawnedIsTreasure.Add(false);
                spawnedPositions.Add(tutRoom.transform.position);
                spawnedRotations.Add(tutRoom.transform.rotation);
                spawnedUsedEntry.Add(tutEntrance);
                branchRoomsPerMain.Add(new List<GameObject>());
            }

            int firstPrefabIndex = Random.Range(0, roomPrefabs.Length);
            GameObject firstRoom = Instantiate(roomPrefabs[firstPrefabIndex], Vector3.zero, Quaternion.identity);

            List<Transform> firstPoints = GetMainConnectionPoints(firstRoom);
            Transform firstEntry = firstPoints.Count > 0 ? firstPoints[0] : firstRoom.transform.Find("Entrence");

            AlignRoom(firstRoom, firstEntry, currentExit);

            placedMultiBounds.Add(GetMultiBounds(firstRoom));
            firstRoom.SetActive(false);

            spawnedRooms.Add(firstRoom);
            spawnedPrefabIndices.Add(firstPrefabIndex);
            spawnedIsTreasure.Add(false);
            spawnedPositions.Add(firstRoom.transform.position);
            spawnedRotations.Add(firstRoom.transform.rotation);
            spawnedUsedEntry.Add(firstEntry);
            branchRoomsPerMain.Add(new List<GameObject>());

            int normalTarget = roomCount + (hasTutorialRoom ? 1 : 0);

            yield return StartCoroutine(BuildNormalChainCoroutine(normalTarget));

            if (!buildChainResult)
                continue;

            yield return StartCoroutine(BuildTreasureRoomsCoroutine());

            if (!buildChainResult)
                continue;

            yield return StartCoroutine(ValidateEndRoomCoroutine());

            if (buildChainResult)
            {
                yield return StartCoroutine(BuildBranchesCoroutine());

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

    private IEnumerator BuildBranchesCoroutine()
    {
        for (int mainIdx = 0; mainIdx < spawnedRooms.Count; mainIdx++)
        {
            if (spawnedRooms[mainIdx] == null) continue;

            List<Transform> branchPoints = GetBranchOnlyPoints(spawnedRooms[mainIdx], spawnedUsedEntry[mainIdx]);
            Transform mainExit = GetExitPoint(spawnedRooms[mainIdx], spawnedUsedEntry[mainIdx]);

            for (int bp = 0; bp < branchPoints.Count; bp++)
            {
                if (branchPoints[bp] == mainExit) continue;

                yield return StartCoroutine(BuildSingleBranch(branchPoints[bp], mainIdx));
            }

            List<Transform> unusedMain = GetUnusedNonBranchPoints(spawnedRooms[mainIdx], spawnedUsedEntry[mainIdx]);
            for (int up = 0; up < unusedMain.Count; up++)
            {
                if (unusedMain[up] == mainExit) continue;

                yield return StartCoroutine(BuildSingleBranch(unusedMain[up], mainIdx));
            }
        }
    }

    private IEnumerator BuildSingleBranch(Transform branchExit, int parentMainIdx)
    {
        Transform currentExit = branchExit;

        for (int depth = 0; depth < maxBranchDepth; depth++)
        {
            List<int> candidates = new List<int>();
            for (int p = 0; p < roomPrefabs.Length; p++)
                candidates.Add(p);

            for (int s = candidates.Count - 1; s > 0; s--)
            {
                int r = Random.Range(0, s + 1);
                int tmp = candidates[s];
                candidates[s] = candidates[r];
                candidates[r] = tmp;
            }

            bool placed = false;
            int boundsIndex = placedMultiBounds.Count;

            foreach (int prefabIndex in candidates)
            {
                GameObject newRoom = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);

                Transform usedEntry;
                if (TryPlaceRoom(newRoom, currentExit, boundsIndex, out usedEntry))
                {
                    List<Bounds> newMultiBounds = GetMultiBounds(newRoom);
                    newRoom.SetActive(false);
                    placedMultiBounds.Add(newMultiBounds);
                    branchRoomsPerMain[parentMainIdx].Add(newRoom);

                    currentExit = GetExitPoint(newRoom, usedEntry);
                    placed = true;
                    break;
                }

                Destroy(newRoom);
            }

            if (!placed) break;

            yield return null;
        }
    }

    private Transform GetLastExit()
    {
        int last = spawnedRooms.Count - 1;
        return GetExitPoint(spawnedRooms[last], spawnedUsedEntry[last]);
    }

    private bool TryPlaceRoom(GameObject room, Transform previousExit, int newChainIndex, out Transform usedEntry)
    {
        List<Transform> points = GetAllConnectionPoints(room);
        usedEntry = null;

        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Transform tmp = points[i];
            points[i] = points[j];
            points[j] = tmp;
        }

        for (int i = 0; i < points.Count; i++)
        {
            AlignRoom(room, points[i], previousExit);

            List<Bounds> newMultiBounds = GetMultiBounds(room);

            if (!OverlapsNearby(newMultiBounds, newChainIndex))
            {
                usedEntry = points[i];
                return true;
            }
        }

        return false;
    }

    private bool TryPlaceRoomMainOnly(GameObject room, Transform previousExit, int newChainIndex, out Transform usedEntry)
    {
        List<Transform> points = GetMainConnectionPoints(room);
        usedEntry = null;

        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Transform tmp = points[i];
            points[i] = points[j];
            points[j] = tmp;
        }

        for (int i = 0; i < points.Count; i++)
        {
            AlignRoom(room, points[i], previousExit);

            List<Bounds> newMultiBounds = GetMultiBounds(room);

            if (!OverlapsNearby(newMultiBounds, newChainIndex))
            {
                usedEntry = points[i];
                return true;
            }
        }

        return false;
    }

    private IEnumerator ValidateEndRoomCoroutine()
    {
        buildChainResult = false;
        if (endRoomPrefab == null || spawnedRooms.Count == 0)
        {
            buildChainResult = true;
            yield break;
        }

        int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (spawnedRooms.Count == 0)
            {
                buildChainResult = false;
                yield break;
            }

            Transform lastExit = GetLastExit();
            GameObject testEnd = Instantiate(endRoomPrefab, Vector3.zero, Quaternion.identity);

            int newChainIndex = placedMultiBounds.Count;
            Transform usedEntry;

            if (TryPlaceRoom(testEnd, lastExit, newChainIndex, out usedEntry))
            {
                testEnd.SetActive(false);
                endRoom = testEnd;
                buildChainResult = true;
                yield break;
            }

            Destroy(testEnd);

            int last = spawnedRooms.Count - 1;
            Destroy(spawnedRooms[last]);
            spawnedRooms.RemoveAt(last);
            placedMultiBounds.RemoveAt(last + 1);
            spawnedPrefabIndices.RemoveAt(last);
            spawnedIsTreasure.RemoveAt(last);
            spawnedPositions.RemoveAt(last);
            spawnedRotations.RemoveAt(last);
            spawnedUsedEntry.RemoveAt(last);
            branchRoomsPerMain.RemoveAt(last);

            yield return null;
        }

        buildChainResult = false;
    }

    private void BuildAllNavMeshes()
    {
        for (int i = 0; i < spawnedRooms.Count; i++)
        {
            if (spawnedRooms[i] == null) continue;
            spawnedRooms[i].SetActive(true);
            NavMeshSurface surface = spawnedRooms[i].GetComponentInChildren<NavMeshSurface>();
            if (surface != null)
                surface.BuildNavMesh();
            spawnedRooms[i].SetActive(false);

            List<GameObject> branches = branchRoomsPerMain[i];
            for (int b = 0; b < branches.Count; b++)
            {
                if (branches[b] == null) continue;
                branches[b].SetActive(true);
                NavMeshSurface bSurface = branches[b].GetComponentInChildren<NavMeshSurface>();
                if (bSurface != null)
                    bSurface.BuildNavMesh();
                branches[b].SetActive(false);
            }
        }

        if (endRoom != null)
        {
            endRoom.SetActive(true);
            NavMeshSurface surface = endRoom.GetComponentInChildren<NavMeshSurface>();
            if (surface != null)
                surface.BuildNavMesh();
            endRoom.SetActive(false);
        }
    }

    private void InitRoomActivator()
    {
        if (roomActivator == null)
            roomActivator = gameObject.AddComponent<RoomActivator>();
        roomActivator.Init(spawnedRooms, startRoom, endRoom, branchRoomsPerMain);
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
            Transform previousExit = GetLastExit();

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
                placedMultiBounds.RemoveAt(last + 1);
                spawnedPrefabIndices.RemoveAt(last);
                spawnedIsTreasure.RemoveAt(last);
                spawnedPositions.RemoveAt(last);
                spawnedRotations.RemoveAt(last);
                spawnedUsedEntry.RemoveAt(last);
                branchRoomsPerMain.RemoveAt(last);

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

            int newChainIndex = placedMultiBounds.Count;

            bool placed = false;
            foreach (int prefabIndex in candidates)
            {
                GameObject newRoom = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);

                Transform usedEntry;
                if (TryPlaceRoomMainOnly(newRoom, previousExit, newChainIndex, out usedEntry))
                {
                    List<Bounds> newMultiBounds = GetMultiBounds(newRoom);
                    newRoom.SetActive(false);
                    spawnedRooms.Add(newRoom);
                    placedMultiBounds.Add(newMultiBounds);
                    spawnedPrefabIndices.Add(prefabIndex);
                    spawnedIsTreasure.Add(false);
                    spawnedPositions.Add(newRoom.transform.position);
                    spawnedRotations.Add(newRoom.transform.rotation);
                    spawnedUsedEntry.Add(usedEntry);
                    branchRoomsPerMain.Add(new List<GameObject>());
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

            Transform previousExit = GetLastExit();
            int newChainIndex = placedMultiBounds.Count;

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

                Transform usedEntry;
                if (TryPlaceRoomMainOnly(newRoom, previousExit, newChainIndex, out usedEntry))
                {
                    List<Bounds> newMultiBounds = GetMultiBounds(newRoom);
                    newRoom.SetActive(false);
                    spawnedRooms.Add(newRoom);
                    placedMultiBounds.Add(newMultiBounds);
                    spawnedPrefabIndices.Add(prefabIndex);
                    spawnedIsTreasure.Add(true);
                    spawnedPositions.Add(newRoom.transform.position);
                    spawnedRotations.Add(newRoom.transform.rotation);
                    spawnedUsedEntry.Add(usedEntry);
                    branchRoomsPerMain.Add(new List<GameObject>());
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
                    previousExit = GetLastExit();
                    newChainIndex = placedMultiBounds.Count;

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

                        Transform usedEntry;
                        if (TryPlaceRoomMainOnly(spacer, previousExit, newChainIndex, out usedEntry))
                        {
                            List<Bounds> spacerMultiBounds = GetMultiBounds(spacer);
                            spacer.SetActive(false);
                            spawnedRooms.Add(spacer);
                            placedMultiBounds.Add(spacerMultiBounds);
                            spawnedPrefabIndices.Add(prefabIndex);
                            spawnedIsTreasure.Add(false);
                            spawnedPositions.Add(spacer.transform.position);
                            spawnedRotations.Add(spacer.transform.rotation);
                            spawnedUsedEntry.Add(usedEntry);
                            branchRoomsPerMain.Add(new List<GameObject>());
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

        Transform lastExit = GetLastExit();
        endRoom = Instantiate(endRoomPrefab, Vector3.zero, Quaternion.identity);

        int newChainIndex = placedMultiBounds.Count;
        Transform usedEntry;

        if (TryPlaceRoom(endRoom, lastExit, newChainIndex, out usedEntry))
        {
            endRoom.SetActive(false);
        }
        else
        {
            Destroy(endRoom);
            endRoom = null;
        }
    }

    private List<Bounds> GetMultiBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            List<Bounds> fallback = new List<Bounds>();
            fallback.Add(new Bounds(room.transform.position, Vector3.one));
            return fallback;
        }

        return RoomBoundsUtil.GenerateMultiBounds(renderers, boundsGridCellSize);
    }

    private bool OverlapsNearby(List<Bounds> newBoundsList, int newChainIndex)
    {
        int checkMin = Mathf.Max(0, newChainIndex - overlapWindow);
        int checkMax = newChainIndex - 2;

        if (checkMin > checkMax) return false;

        for (int n = 0; n < newBoundsList.Count; n++)
        {
            for (int p = checkMin; p <= checkMax; p++)
            {
                if (p < 0 || p >= placedMultiBounds.Count) continue;

                List<Bounds> existingList = placedMultiBounds[p];
                for (int e = 0; e < existingList.Count; e++)
                {
                    if (newBoundsList[n].Intersects(existingList[e]))
                        return true;
                }
            }
        }

        return false;
    }

    public void ClearRooms()
    {
        for (int i = 0; i < branchRoomsPerMain.Count; i++)
        {
            for (int b = 0; b < branchRoomsPerMain[i].Count; b++)
            {
                if (branchRoomsPerMain[i][b] != null)
                    Destroy(branchRoomsPerMain[i][b]);
            }
        }

        foreach (GameObject room in spawnedRooms)
        {
            if (room != null)
                Destroy(room);
        }
        if (endRoom != null)
            Destroy(endRoom);

        spawnedRooms.Clear();
        placedMultiBounds.Clear();
        spawnedPrefabIndices.Clear();
        spawnedIsTreasure.Clear();
        spawnedPositions.Clear();
        spawnedRotations.Clear();
        spawnedUsedEntry.Clear();
        branchRoomsPerMain.Clear();
        endRoom = null;
    }
}