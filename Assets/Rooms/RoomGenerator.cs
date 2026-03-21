using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;

public class RoomGenerator : MonoBehaviour
{
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed;
    [SerializeField] private int minRoomCount = 45;
    [SerializeField] private int maxRoomCount = 50;
    [SerializeField] private GameObject startRoomPrefab;
    [SerializeField] private GameObject tutorialRoomPrefab;
    [SerializeField] private GameObject[] roomPrefabs;
    [SerializeField] private GameObject[] treasureRoomPrefabs;
    [SerializeField] private GameObject bossRoomPrefab;
    [SerializeField] private LoadingScreenController loadingScreen;
    [SerializeField] private int treasureRoomsOnMainPath = 2;
    [SerializeField] private int treasureRoomsOnBranches = 0;
    [SerializeField] private float boundsShrink = 0.25f;
    [SerializeField] private int overlapWindow = 2;
    [SerializeField] private float connectionSnapThreshold = 0.5f;
    [SerializeField] private float mainChainFraction = 0.6f;
    [SerializeField] private float loopConnectionDistance = 2f;

    public int CurrentSeed => seed;

    private RoomActivator roomActivator;
    private GameObject startRoom;
    private List<Bounds> startMultiBounds;
    private bool hasTutorialRoom;
    private bool buildChainResult;

    private List<GameObject> allRooms = new List<GameObject>();
    private List<List<Bounds>> placedMultiBounds = new List<List<Bounds>>();
    private HashSet<Transform> loopConnectedPoints = new HashSet<Transform>();
    private List<KeyValuePair<GameObject, GameObject>> loopAdjacency = new List<KeyValuePair<GameObject, GameObject>>();

    // ── Adjacency built at placement time ──────────────────────────────────────
    // Key = room GameObject, Value = list of directly connected room GameObjects.
    // startRoom is included once it's known.
    private Dictionary<GameObject, List<GameObject>> roomAdjacency
        = new Dictionary<GameObject, List<GameObject>>();

    private struct PlacedRoom
    {
        public GameObject go;
        public Transform usedEntry;
        public int prefabIndex;
        public bool isTreasure;
    }

    private List<PlacedRoom> mainChain = new List<PlacedRoom>();
    private Dictionary<int, HashSet<int>> failedPrefabsPerStep = new Dictionary<int, HashSet<int>>();
    private HashSet<int> treasureInsertIndices = new HashSet<int>();

    // ── Adjacency helpers ───────────────────────────────────────────────────────

    private void EnsureAdjacencyEntry(GameObject room)
    {
        if (room != null && !roomAdjacency.ContainsKey(room))
            roomAdjacency[room] = new List<GameObject>();
    }

    /// <summary>Register a bidirectional connection between two rooms.</summary>
    private void ConnectRooms(GameObject a, GameObject b)
    {
        if (a == null || b == null) return;
        EnsureAdjacencyEntry(a);
        EnsureAdjacencyEntry(b);
        if (!roomAdjacency[a].Contains(b)) roomAdjacency[a].Add(b);
        if (!roomAdjacency[b].Contains(a)) roomAdjacency[b].Add(a);
    }

    // ── Bounds helpers ──────────────────────────────────────────────────────────

    private List<Bounds> GetMultiBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new List<Bounds> { new Bounds(room.transform.position, Vector3.one) };

        List<Bounds> result = new List<Bounds>(renderers.Length);
        Vector3 shrink = Vector3.one * (boundsShrink * 2f);
        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            if (b.size.x < 0.01f || b.size.y < 0.01f || b.size.z < 0.01f) continue;
            b.size -= shrink;
            if (b.size.x > 0 && b.size.y > 0 && b.size.z > 0)
                result.Add(b);
        }
        if (result.Count == 0)
            result.Add(new Bounds(room.transform.position, Vector3.one));
        return result;
    }

    private bool OverlapsNearby(List<Bounds> newBounds, int newChainIndex)
    {
        int checkMin = Mathf.Max(0, newChainIndex - overlapWindow);
        int checkMax = newChainIndex - 2;
        if (checkMin > checkMax) return false;
        for (int n = 0; n < newBounds.Count; n++)
            for (int p = checkMin; p <= checkMax; p++)
            {
                if (p < 0 || p >= placedMultiBounds.Count) continue;
                List<Bounds> existing = placedMultiBounds[p];
                for (int e = 0; e < existing.Count; e++)
                    if (newBounds[n].Intersects(existing[e])) return true;
            }
        return false;
    }

    private bool OverlapsAll(List<Bounds> newBounds, int excludeIdx)
    {
        for (int p = 0; p < placedMultiBounds.Count; p++)
        {
            if (p == excludeIdx) continue;
            List<Bounds> existing = placedMultiBounds[p];
            for (int n = 0; n < newBounds.Count; n++)
                for (int e = 0; e < existing.Count; e++)
                    if (newBounds[n].Intersects(existing[e])) return true;
        }
        return false;
    }

    // ── Connection-point helpers ────────────────────────────────────────────────

    private List<Transform> GetConnectionPoints(GameObject room, bool mainOnly)
    {
        List<Transform> points = new List<Transform>();
        Transform root = room.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != "Entrence" && child.name != "Exit") continue;
            if (mainOnly && child.GetComponent<BranchPoint>() != null) continue;
            points.Add(child);
        }
        return points;
    }

    private List<Transform> GetAllPoints(GameObject room) => GetConnectionPoints(room, false);
    private List<Transform> GetMainPoints(GameObject room) => GetConnectionPoints(room, true);

    private Transform GetFarthestPoint(GameObject room, Transform from, bool mainOnly)
    {
        List<Transform> pts = mainOnly ? GetMainPoints(room) : GetAllPoints(room);
        List<Transform> candidates = new List<Transform>();
        for (int i = 0; i < pts.Count; i++)
            if (pts[i] != from) candidates.Add(pts[i]);

        if (candidates.Count == 0)
        {
            List<Transform> all = GetAllPoints(room);
            for (int i = 0; i < all.Count; i++)
                if (all[i] != from) candidates.Add(all[i]);
        }

        if (candidates.Count == 0)
        {
            List<Transform> all = GetAllPoints(room);
            return all.Count > 0 ? all[0] : null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void AlignRoom(GameObject room, Transform roomEntry, Transform targetExit)
    {
        Quaternion targetRot = Quaternion.LookRotation(-targetExit.forward, Vector3.up);
        Quaternion rotDiff = targetRot * Quaternion.Inverse(roomEntry.rotation);
        room.transform.rotation = rotDiff * room.transform.rotation;
        room.transform.position += targetExit.position - roomEntry.position;
    }

    private bool TryPlaceMainOnly(GameObject room, Transform previousExit, int newChainIndex, out Transform usedEntry)
    {
        List<Transform> points = GetMainPoints(room);
        usedEntry = null;
        List<Transform> validEntries = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            AlignRoom(room, points[i], previousExit);
            if (!OverlapsNearby(GetMultiBounds(room), newChainIndex))
                validEntries.Add(points[i]);
        }
        if (validEntries.Count == 0) return false;
        usedEntry = validEntries[Random.Range(0, validEntries.Count)];
        AlignRoom(room, usedEntry, previousExit);
        return true;
    }

    private bool TryPlaceAny(GameObject room, Transform previousExit, int newChainIndex, out Transform usedEntry)
    {
        List<Transform> points = GetAllPoints(room);
        usedEntry = null;
        List<Transform> validEntries = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            AlignRoom(room, points[i], previousExit);
            if (!OverlapsNearby(GetMultiBounds(room), newChainIndex))
                validEntries.Add(points[i]);
        }
        if (validEntries.Count == 0) return false;
        usedEntry = validEntries[Random.Range(0, validEntries.Count)];
        AlignRoom(room, usedEntry, previousExit);
        return true;
    }

    private bool TryPlaceBranch(GameObject room, Transform previousExit, int parentBoundsIdx, out Transform usedEntry)
    {
        List<Transform> points = GetAllPoints(room);
        usedEntry = null;
        List<Transform> validEntries = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            AlignRoom(room, points[i], previousExit);
            if (!OverlapsAll(GetMultiBounds(room), parentBoundsIdx))
                validEntries.Add(points[i]);
        }
        if (validEntries.Count == 0) return false;
        usedEntry = validEntries[Random.Range(0, validEntries.Count)];
        AlignRoom(room, usedEntry, previousExit);
        return true;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private HashSet<int> PickRandomIndices(int count, int minIdx, int maxIdx)
    {
        HashSet<int> result = new HashSet<int>();
        if (maxIdx < minIdx || count <= 0) return result;
        List<int> pool = new List<int>();
        for (int i = minIdx; i <= maxIdx; i++) pool.Add(i);
        Shuffle(pool);
        for (int i = 0; i < Mathf.Min(count, pool.Count); i++)
            result.Add(pool[i]);
        return result;
    }

    // ── Generate ────────────────────────────────────────────────────────────────

    public IEnumerator Generate()
    {
        if (useRandomSeed)
            seed = System.Environment.TickCount ^ System.DateTime.Now.Millisecond;
        Random.InitState(seed);

        int totalRoomBudget = Random.Range(minRoomCount, maxRoomCount + 1);
        int maxFullRetries = 67;

        startRoom = Instantiate(startRoomPrefab, transform.position, transform.rotation);
        startMultiBounds = GetMultiBounds(startRoom);
        EnsureAdjacencyEntry(startRoom);

        Transform startExit = startRoom.transform.Find("Exit");
        hasTutorialRoom = tutorialRoomPrefab != null;

        int mainChainTarget = Mathf.Max(3, Mathf.RoundToInt(totalRoomBudget * mainChainFraction));
        if (hasTutorialRoom) mainChainTarget++;

        for (int retry = 0; retry < maxFullRetries; retry++)
        {
            ClearAll();
            EnsureAdjacencyEntry(startRoom);
            placedMultiBounds.Add(startMultiBounds);

            int firstValidIdx = hasTutorialRoom ? 2 : 1;
            int lastValidIdx = mainChainTarget - 3;
            treasureInsertIndices = PickRandomIndices(treasureRoomsOnMainPath, firstValidIdx, lastValidIdx);

            Transform currentExit = startExit;

            if (hasTutorialRoom)
            {
                GameObject tutRoom = Instantiate(tutorialRoomPrefab, Vector3.zero, Quaternion.identity);
                Transform tutEntrance = tutRoom.transform.Find("Entrence");
                AlignRoom(tutRoom, tutEntrance, currentExit);
                placedMultiBounds.Add(GetMultiBounds(tutRoom));
                currentExit = GetFarthestPoint(tutRoom, tutEntrance, true);
                tutRoom.SetActive(false);
                mainChain.Add(new PlacedRoom { go = tutRoom, usedEntry = tutEntrance, prefabIndex = -1 });
                allRooms.Add(tutRoom);
                // startRoom → tutRoom
                ConnectRooms(startRoom, tutRoom);
            }

            int firstIdx = Random.Range(0, roomPrefabs.Length);
            GameObject firstRoom = Instantiate(roomPrefabs[firstIdx], Vector3.zero, Quaternion.identity);
            List<Transform> firstPts = GetMainPoints(firstRoom);
            Shuffle(firstPts);
            Transform firstEntry = firstPts.Count > 0 ? firstPts[0] : firstRoom.transform.Find("Entrence");
            AlignRoom(firstRoom, firstEntry, currentExit);
            placedMultiBounds.Add(GetMultiBounds(firstRoom));
            firstRoom.SetActive(false);
            mainChain.Add(new PlacedRoom { go = firstRoom, usedEntry = firstEntry, prefabIndex = firstIdx });
            allRooms.Add(firstRoom);

            // Connect firstRoom to whatever is before it in the chain.
            GameObject prevRoom = hasTutorialRoom ? mainChain[0].go : startRoom;
            ConnectRooms(prevRoom, firstRoom);

            yield return StartCoroutine(BuildMainChain(mainChainTarget));
            if (!buildChainResult) continue;

            yield return StartCoroutine(PlaceBossRoom());
            if (!buildChainResult) continue;

            int remainingBudget = totalRoomBudget - allRooms.Count;
            if (remainingBudget > 0)
                yield return StartCoroutine(BuildBranchesRecursive(remainingBudget));

            FindLoopConnections();
            BuildAllNavMeshes();
            InitAllDoors();
            InitRoomActivator();
            TeleportPlayerToStart();
            if (loadingScreen != null) loadingScreen.StartFadeOut();
            yield break;
        }

        // Fallback if all retries failed.
        FindLoopConnections();
        BuildAllNavMeshes();
        InitAllDoors();
        InitRoomActivator();
        TeleportPlayerToStart();
        if (loadingScreen != null) loadingScreen.StartFadeOut();
    }

    private void TeleportPlayerToStart()
    {
        if (startRoom == null) return;
        startRoom.SetActive(true);
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;
        Transform spawnPoint = startRoom.transform.Find("Start");
        if (spawnPoint == null) return;
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerObj.transform.position = spawnPoint.position;
        if (cc != null) cc.enabled = true;
        if (roomActivator != null) roomActivator.OnPlayerEnteredRoom(startRoom);
    }

    private Transform GetLastMainExit()
    {
        PlacedRoom last = mainChain[mainChain.Count - 1];
        return GetFarthestPoint(last.go, last.usedEntry, true);
    }

    // ── Main chain ──────────────────────────────────────────────────────────────

    private IEnumerator BuildMainChain(int targetCount)
    {
        buildChainResult = false;
        int minKeep = hasTutorialRoom ? 2 : 1;
        failedPrefabsPerStep.Clear();
        int totalBacktracks = 0;
        int maxBacktracks = targetCount * 10;

        while (mainChain.Count < targetCount)
        {
            int step = mainChain.Count;
            Transform prevExit = GetLastMainExit();
            GameObject parentRoom = mainChain[mainChain.Count - 1].go;

            // ── Treasure room slot? ────────────────────────────────────────────
            if (treasureInsertIndices.Contains(step)
                && treasureRoomPrefabs != null && treasureRoomPrefabs.Length > 0)
            {
                int newChainIndex = placedMultiBounds.Count;
                List<int> treasureCandidates = new List<int>();
                for (int p = 0; p < treasureRoomPrefabs.Length; p++) treasureCandidates.Add(p);
                Shuffle(treasureCandidates);

                bool treasurePlaced = false;
                foreach (int prefabIndex in treasureCandidates)
                {
                    GameObject tRoom = Instantiate(treasureRoomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                    Transform usedEntry;
                    if (TryPlaceMainOnly(tRoom, prevExit, newChainIndex, out usedEntry))
                    {
                        tRoom.SetActive(false);
                        placedMultiBounds.Add(GetMultiBounds(tRoom));
                        mainChain.Add(new PlacedRoom { go = tRoom, usedEntry = usedEntry, prefabIndex = prefabIndex, isTreasure = true });
                        allRooms.Add(tRoom);
                        ConnectRooms(parentRoom, tRoom);
                        treasurePlaced = true;
                        break;
                    }
                    Destroy(tRoom);
                }

                if (treasurePlaced) { yield return null; continue; }
            }

            // ── Regular room ───────────────────────────────────────────────────
            if (!failedPrefabsPerStep.ContainsKey(step))
                failedPrefabsPerStep[step] = new HashSet<int>();
            HashSet<int> failedHere = failedPrefabsPerStep[step];

            if (failedHere.Count >= roomPrefabs.Length)
            {
                if (mainChain.Count <= minKeep) { buildChainResult = false; yield break; }
                failedHere.Clear();

                int last = mainChain.Count - 1;
                if (!failedPrefabsPerStep.ContainsKey(last))
                    failedPrefabsPerStep[last] = new HashSet<int>();
                failedPrefabsPerStep[last].Add(mainChain[last].prefabIndex);

                // Remove adjacency for the room being backtracked.
                RemoveAdjacency(mainChain[last].go);
                Destroy(mainChain[last].go);
                allRooms.Remove(mainChain[last].go);
                mainChain.RemoveAt(last);
                placedMultiBounds.RemoveAt(last + 1);

                totalBacktracks++;
                if (totalBacktracks > maxBacktracks) { buildChainResult = false; yield break; }
                yield return null;
                continue;
            }

            List<int> candidates = new List<int>();
            for (int p = 0; p < roomPrefabs.Length; p++)
                if (!failedHere.Contains(p)) candidates.Add(p);
            Shuffle(candidates);

            int newIdx = placedMultiBounds.Count;
            bool placed = false;

            foreach (int prefabIndex in candidates)
            {
                GameObject newRoom = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                Transform usedEntry;
                if (TryPlaceMainOnly(newRoom, prevExit, newIdx, out usedEntry))
                {
                    newRoom.SetActive(false);
                    placedMultiBounds.Add(GetMultiBounds(newRoom));
                    mainChain.Add(new PlacedRoom { go = newRoom, usedEntry = usedEntry, prefabIndex = prefabIndex });
                    allRooms.Add(newRoom);
                    ConnectRooms(parentRoom, newRoom);
                    placed = true;
                    break;
                }
                failedHere.Add(prefabIndex);
                Destroy(newRoom);
            }

            if (!placed) { yield return null; continue; }
            yield return null;
        }

        buildChainResult = true;
    }

    // ── Boss room ───────────────────────────────────────────────────────────────

    private IEnumerator PlaceBossRoom()
    {
        buildChainResult = false;
        if (bossRoomPrefab == null) { buildChainResult = true; yield break; }

        int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Transform lastExit = GetLastMainExit();
            GameObject parentRoom = mainChain[mainChain.Count - 1].go;
            GameObject boss = Instantiate(bossRoomPrefab, Vector3.zero, Quaternion.identity);
            int parentBoundsIdx = placedMultiBounds.Count - 1;
            Transform usedEntry;

            if (TryPlaceBranch(boss, lastExit, parentBoundsIdx, out usedEntry))
            {
                boss.SetActive(false);
                placedMultiBounds.Add(GetMultiBounds(boss));
                mainChain.Add(new PlacedRoom { go = boss, usedEntry = usedEntry, prefabIndex = -1 });
                allRooms.Add(boss);
                ConnectRooms(parentRoom, boss);
                buildChainResult = true;
                yield break;
            }

            Destroy(boss);

            int minKeep = hasTutorialRoom ? 2 : 1;
            if (mainChain.Count <= minKeep) { buildChainResult = false; yield break; }

            int last = mainChain.Count - 1;
            RemoveAdjacency(mainChain[last].go);
            Destroy(mainChain[last].go);
            allRooms.Remove(mainChain[last].go);
            mainChain.RemoveAt(last);
            placedMultiBounds.RemoveAt(last + 1);

            yield return null;
        }

        buildChainResult = false;
    }

    // ── Branches ────────────────────────────────────────────────────────────────

    private struct BranchTask
    {
        public Transform exitPoint;
        public int parentBoundsIdx;
        public GameObject parentRoom;   // ← new: who owns this exit point
    }

    private IEnumerator BuildBranchesRecursive(int budget)
    {
        int roomsLeft = budget;
        int branchTreasuresLeft = treasureRoomsOnBranches;
        List<BranchTask> branchTasks = new List<BranchTask>();

        for (int i = 0; i < mainChain.Count; i++)
        {
            if (mainChain[i].go == null) continue;
            int boundsIdx = i + 1;
            List<Transform> openPts = GetOpenConnectionPoints(mainChain[i].go, mainChain[i].usedEntry);
            for (int j = 0; j < openPts.Count; j++)
                branchTasks.Add(new BranchTask
                {
                    exitPoint = openPts[j],
                    parentBoundsIdx = boundsIdx,
                    parentRoom = mainChain[i].go
                });
        }

        Shuffle(branchTasks);

        while (branchTasks.Count > 0 && roomsLeft > 0)
        {
            int taskIdx = Random.Range(0, branchTasks.Count);
            BranchTask task = branchTasks[taskIdx];
            branchTasks.RemoveAt(taskIdx);

            // ── Try treasure branch ────────────────────────────────────────────
            bool tryTreasureHere = branchTreasuresLeft > 0
                && treasureRoomPrefabs != null
                && treasureRoomPrefabs.Length > 0
                && Random.Range(0, branchTasks.Count + 1) < branchTreasuresLeft;

            if (tryTreasureHere)
            {
                List<int> tCandidates = new List<int>();
                for (int p = 0; p < treasureRoomPrefabs.Length; p++) tCandidates.Add(p);
                Shuffle(tCandidates);

                foreach (int prefabIndex in tCandidates)
                {
                    GameObject tRoom = Instantiate(treasureRoomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                    Transform usedEntry;
                    if (TryPlaceBranch(tRoom, task.exitPoint, task.parentBoundsIdx, out usedEntry))
                    {
                        tRoom.SetActive(false);
                        int newBoundsIdx = placedMultiBounds.Count;
                        placedMultiBounds.Add(GetMultiBounds(tRoom));
                        allRooms.Add(tRoom);
                        ConnectRooms(task.parentRoom, tRoom);
                        roomsLeft--;
                        branchTreasuresLeft--;

                        if (roomsLeft > 0)
                        {
                            List<Transform> openPts = GetOpenConnectionPoints(tRoom, usedEntry);
                            for (int j = 0; j < openPts.Count; j++)
                                branchTasks.Add(new BranchTask
                                {
                                    exitPoint = openPts[j],
                                    parentBoundsIdx = newBoundsIdx,
                                    parentRoom = tRoom
                                });
                        }
                        goto nextTask;
                    }
                    Destroy(tRoom);
                }
            }

            // ── Regular branch ─────────────────────────────────────────────────
            {
                List<int> candidates = new List<int>();
                for (int p = 0; p < roomPrefabs.Length; p++) candidates.Add(p);
                Shuffle(candidates);

                foreach (int prefabIndex in candidates)
                {
                    GameObject newRoom = Instantiate(roomPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
                    Transform usedEntry;
                    if (TryPlaceBranch(newRoom, task.exitPoint, task.parentBoundsIdx, out usedEntry))
                    {
                        newRoom.SetActive(false);
                        int newBoundsIdx = placedMultiBounds.Count;
                        placedMultiBounds.Add(GetMultiBounds(newRoom));
                        allRooms.Add(newRoom);
                        ConnectRooms(task.parentRoom, newRoom);
                        roomsLeft--;

                        if (roomsLeft > 0)
                        {
                            List<Transform> openPts = GetOpenConnectionPoints(newRoom, usedEntry);
                            for (int j = 0; j < openPts.Count; j++)
                                branchTasks.Add(new BranchTask
                                {
                                    exitPoint = openPts[j],
                                    parentBoundsIdx = newBoundsIdx,
                                    parentRoom = newRoom
                                });
                        }
                        break;
                    }
                    Destroy(newRoom);
                }
            }

        nextTask:
            yield return null;
        }
    }

    // ── Open connection points ──────────────────────────────────────────────────

    private List<Transform> GetOpenConnectionPoints(GameObject room, Transform usedEntry)
    {
        List<Transform> points = new List<Transform>();
        Transform root = room.transform;

        bool wasActive = room.activeSelf;
        room.SetActive(true);

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != "Entrence" && child.name != "Exit") continue;
            if (child == usedEntry) continue;

            bool isConnected = false;
            for (int r = 0; r < allRooms.Count; r++)
            {
                if (allRooms[r] == null || allRooms[r] == room) continue;
                bool otherWasActive = allRooms[r].activeSelf;
                allRooms[r].SetActive(true);
                List<Transform> otherPts = GetAllPoints(allRooms[r]);
                for (int k = 0; k < otherPts.Count; k++)
                {
                    if (Vector3.Distance(child.position, otherPts[k].position) <= connectionSnapThreshold)
                    {
                        isConnected = true;
                        break;
                    }
                }
                allRooms[r].SetActive(otherWasActive);
                if (isConnected) break;
            }

            if (!isConnected)
                points.Add(child);
        }

        room.SetActive(wasActive);
        Shuffle(points);
        return points;
    }

    // ── Loop connections ────────────────────────────────────────────────────────

    private void FindLoopConnections()
    {
        loopConnectedPoints.Clear();
        loopAdjacency.Clear();

        List<GameObject> everyRoom = new List<GameObject>();
        if (startRoom != null) everyRoom.Add(startRoom);
        everyRoom.AddRange(allRooms);

        List<(Transform point, GameObject room)> openPoints = new List<(Transform, GameObject)>();

        for (int i = 0; i < everyRoom.Count; i++)
        {
            if (everyRoom[i] == null) continue;
            bool wasActive = everyRoom[i].activeSelf;
            everyRoom[i].SetActive(true);
            List<Transform> pts = GetAllPoints(everyRoom[i]);

            for (int j = 0; j < pts.Count; j++)
            {
                bool connected = false;
                for (int r = 0; r < everyRoom.Count; r++)
                {
                    if (r == i || everyRoom[r] == null) continue;
                    bool otherWas = everyRoom[r].activeSelf;
                    everyRoom[r].SetActive(true);
                    List<Transform> otherPts = GetAllPoints(everyRoom[r]);
                    for (int k = 0; k < otherPts.Count; k++)
                    {
                        if (Vector3.Distance(pts[j].position, otherPts[k].position) <= connectionSnapThreshold)
                        {
                            connected = true;
                            break;
                        }
                    }
                    everyRoom[r].SetActive(otherWas);
                    if (connected) break;
                }
                if (!connected)
                    openPoints.Add((pts[j], everyRoom[i]));
            }

            everyRoom[i].SetActive(wasActive);
        }

        HashSet<int> claimed = new HashSet<int>();
        for (int a = 0; a < openPoints.Count; a++)
        {
            if (claimed.Contains(a)) continue;
            for (int b = a + 1; b < openPoints.Count; b++)
            {
                if (claimed.Contains(b)) continue;
                if (openPoints[a].room == openPoints[b].room) continue;

                float dist = Vector3.Distance(openPoints[a].point.position, openPoints[b].point.position);
                if (dist > loopConnectionDistance) continue;

                float dot = Vector3.Dot(openPoints[a].point.forward, openPoints[b].point.forward);
                if (dot > -0.5f) continue;

                loopConnectedPoints.Add(openPoints[a].point);
                loopConnectedPoints.Add(openPoints[b].point);
                loopAdjacency.Add(new KeyValuePair<GameObject, GameObject>(openPoints[a].room, openPoints[b].room));

                // Register loop connections in our adjacency map too.
                ConnectRooms(openPoints[a].room, openPoints[b].room);

                claimed.Add(a);
                claimed.Add(b);
                break;
            }
        }
    }

    // ── Nav mesh ────────────────────────────────────────────────────────────────

    private void BuildAllNavMeshes()
    {
        if (startRoom != null)
        {
            startRoom.SetActive(true);
            NavMeshSurface s = startRoom.GetComponentInChildren<NavMeshSurface>();
            if (s != null) s.BuildNavMesh();
            startRoom.SetActive(false);
        }

        for (int i = 0; i < allRooms.Count; i++)
        {
            if (allRooms[i] == null) continue;
            allRooms[i].SetActive(true);
            NavMeshSurface surface = allRooms[i].GetComponentInChildren<NavMeshSurface>();
            if (surface != null) surface.BuildNavMesh();
            allRooms[i].SetActive(false);
        }
    }

    // ── Doors ───────────────────────────────────────────────────────────────────

    private void InitAllDoors()
    {
        List<GameObject> everyRoom = new List<GameObject>(allRooms);
        if (startRoom != null) everyRoom.Add(startRoom);

        List<List<Vector3>> pointPositionsByRoom = new List<List<Vector3>>();
        for (int i = 0; i < everyRoom.Count; i++)
        {
            if (everyRoom[i] == null) { pointPositionsByRoom.Add(null); continue; }
            bool wasActive = everyRoom[i].activeSelf;
            everyRoom[i].SetActive(true);
            List<Transform> pts = GetAllPoints(everyRoom[i]);
            List<Vector3> positions = new List<Vector3>(pts.Count);
            for (int j = 0; j < pts.Count; j++)
                positions.Add(pts[j].position);
            pointPositionsByRoom.Add(positions);
            everyRoom[i].SetActive(wasActive);
        }

        for (int i = 0; i < everyRoom.Count; i++)
        {
            if (everyRoom[i] == null) continue;
            Doors doorsComp = everyRoom[i].GetComponent<Doors>();
            if (doorsComp == null) continue;

            bool wasActive = everyRoom[i].activeSelf;
            everyRoom[i].SetActive(true);

            List<Transform> myPoints = GetAllPoints(everyRoom[i]);
            HashSet<Transform> deadEnds = new HashSet<Transform>();

            for (int j = 0; j < myPoints.Count; j++)
            {
                Vector3 myPos = myPoints[j].position;
                bool matched = false;
                for (int r = 0; r < everyRoom.Count; r++)
                {
                    if (r == i || pointPositionsByRoom[r] == null) continue;
                    List<Vector3> otherPositions = pointPositionsByRoom[r];
                    for (int k = 0; k < otherPositions.Count; k++)
                    {
                        if (Vector3.Distance(myPos, otherPositions[k]) <= connectionSnapThreshold)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (matched) break;
                }
                if (!matched && !loopConnectedPoints.Contains(myPoints[j]))
                    deadEnds.Add(myPoints[j]);
            }

            doorsComp.Init(deadEnds);
            everyRoom[i].SetActive(wasActive);
        }
    }

    // ── Room activator ──────────────────────────────────────────────────────────

    private void InitRoomActivator()
    {
        List<GameObject> everyRoom = new List<GameObject>();
        if (startRoom != null) everyRoom.Add(startRoom);
        everyRoom.AddRange(allRooms);

        if (roomActivator == null)
            roomActivator = gameObject.AddComponent<RoomActivator>();

        // Pass the adjacency map we built at placement time — no re-computation.
        roomActivator.Init(everyRoom, roomAdjacency);
    }

    // ── Adjacency cleanup on backtrack ──────────────────────────────────────────

    /// <summary>
    /// Remove a room from the adjacency map entirely and strip it from its neighbors' lists.
    /// Called when backtracking destroys a room.
    /// </summary>
    private void RemoveAdjacency(GameObject room)
    {
        if (room == null || !roomAdjacency.ContainsKey(room)) return;
        List<GameObject> neighbors = roomAdjacency[room];
        for (int i = 0; i < neighbors.Count; i++)
        {
            if (neighbors[i] != null && roomAdjacency.ContainsKey(neighbors[i]))
                roomAdjacency[neighbors[i]].Remove(room);
        }
        roomAdjacency.Remove(room);
    }

    // ── Clear ───────────────────────────────────────────────────────────────────

    private void ClearAll()
    {
        for (int i = 0; i < allRooms.Count; i++)
            if (allRooms[i] != null) Destroy(allRooms[i]);

        allRooms.Clear();
        mainChain.Clear();
        placedMultiBounds.Clear();
        failedPrefabsPerStep.Clear();
        treasureInsertIndices.Clear();
        loopConnectedPoints.Clear();
        loopAdjacency.Clear();
        roomAdjacency.Clear();
    }

    public void ClearRooms()
    {
        ClearAll();
        if (startRoom != null) { Destroy(startRoom); startRoom = null; }
    }
}