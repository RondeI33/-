using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;

public class RoomActivator : MonoBehaviour
{
    private List<GameObject> rooms;
    private List<Bounds> roomBoundsCache;
    private List<Vector3> roomCenters;
    private List<List<GameObject>> branchRooms;
    private GameObject startRoom;
    private GameObject endRoom;
    private int currentRoomIndex = 0;
    private int roomWindow = 2;
    private Transform player;

    public void SetStartRoom(GameObject newStart)
    {
        startRoom = newStart;
    }

    public void Init(List<GameObject> spawnedRooms, GameObject start, GameObject end)
    {
        Init(spawnedRooms, start, end, null);
    }

    public void Init(List<GameObject> spawnedRooms, GameObject start, GameObject end, List<List<GameObject>> branches)
    {
        rooms = spawnedRooms;
        startRoom = start;
        endRoom = end;
        branchRooms = branches;

        roomCenters = new List<Vector3>();
        roomBoundsCache = new List<Bounds>();

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null)
            {
                roomCenters.Add(Vector3.zero);
                roomBoundsCache.Add(new Bounds());
                continue;
            }
            Bounds b = ComputeRoomBounds(rooms[i]);
            roomCenters.Add(b.center);
            roomBoundsCache.Add(b);
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        currentRoomIndex = 0;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null) continue;
            Doors doors = rooms[i].GetComponentInChildren<Doors>();
            if (doors != null)
                StartCoroutine(DelayedRefreshDoors(doors));
        }

        UpdateActiveRooms();
    }

    public void RefreshRooms(List<GameObject> spawnedRooms, GameObject start, GameObject end)
    {
        RefreshRooms(spawnedRooms, start, end, null);
    }

    public void RefreshRooms(List<GameObject> spawnedRooms, GameObject start, GameObject end, List<List<GameObject>> branches)
    {
        rooms = spawnedRooms;
        startRoom = start;
        endRoom = end;
        branchRooms = branches;

        roomCenters = new List<Vector3>();
        roomBoundsCache = new List<Bounds>();

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null)
            {
                roomCenters.Add(Vector3.zero);
                roomBoundsCache.Add(new Bounds());
                continue;
            }
            Bounds b = ComputeRoomBounds(rooms[i]);
            roomCenters.Add(b.center);
            roomBoundsCache.Add(b);
        }

        currentRoomIndex = Mathf.Clamp(currentRoomIndex, 0, rooms.Count - 1);

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null) continue;
            Doors doors = rooms[i].GetComponentInChildren<Doors>();
            if (doors != null)
                StartCoroutine(DelayedRefreshDoors(doors));
        }

        UpdateActiveRooms();
    }

    private void Update()
    {
        if (player == null || rooms == null) return;

        if (rooms[currentRoomIndex] != null && roomBoundsCache[currentRoomIndex].Contains(player.position))
            return;

        int searchMin = Mathf.Max(0, currentRoomIndex - roomWindow);
        int searchMax = Mathf.Min(rooms.Count - 1, currentRoomIndex + roomWindow);

        int best = currentRoomIndex;
        float bestDist = float.MaxValue;

        for (int i = searchMin; i <= searchMax; i++)
        {
            if (rooms[i] == null) continue;
            Bounds b = roomBoundsCache[i];
            if (b.Contains(player.position))
            {
                best = i;
                bestDist = 0f;
                break;
            }
            float dist = b.SqrDistance(player.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        if (best != currentRoomIndex)
        {
            currentRoomIndex = best;
            UpdateActiveRooms();
        }
    }

    private void UpdateActiveRooms()
    {
        int min = currentRoomIndex - roomWindow;
        int max = currentRoomIndex + roomWindow;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null) continue;
            bool shouldBeActive = i >= min && i <= max;

            if (shouldBeActive && !rooms[i].activeSelf)
            {
                rooms[i].SetActive(true);
                OnRoomEnabled(rooms[i]);
                SetBranchRoomsActive(i, true);
            }
            else if (!shouldBeActive && rooms[i].activeSelf)
            {
                OnRoomDisabled(rooms[i]);
                rooms[i].SetActive(false);
                SetBranchRoomsActive(i, false);
            }
        }

        if (startRoom != null && !startRoom.activeSelf)
            startRoom.SetActive(true);

        if (endRoom != null)
        {
            bool endActive = max >= rooms.Count;
            if (endActive && !endRoom.activeSelf)
            {
                endRoom.SetActive(true);
                OnRoomEnabled(endRoom);
            }
            else if (!endActive && endRoom.activeSelf)
            {
                OnRoomDisabled(endRoom);
                endRoom.SetActive(false);
            }
        }
    }

    private void SetBranchRoomsActive(int mainIndex, bool active)
    {
        if (branchRooms == null || mainIndex >= branchRooms.Count) return;

        List<GameObject> branches = branchRooms[mainIndex];
        for (int b = 0; b < branches.Count; b++)
        {
            if (branches[b] == null) continue;

            if (active && !branches[b].activeSelf)
            {
                branches[b].SetActive(true);
                OnRoomEnabled(branches[b]);
            }
            else if (!active && branches[b].activeSelf)
            {
                OnRoomDisabled(branches[b]);
                branches[b].SetActive(false);
            }
        }
    }

    private void OnRoomDisabled(GameObject room)
    {
        Rigidbody[] bodies = room.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!bodies[i].isKinematic)
            {
                bodies[i].linearVelocity = Vector3.zero;
                bodies[i].angularVelocity = Vector3.zero;
                bodies[i].isKinematic = true;
                bodies[i].gameObject.AddComponent<WasNonKinematic>();
            }
        }

        Bounds roomBounds = GetRoomBounds(room);
        GameObject[] pickups = GameObject.FindGameObjectsWithTag("Pickup");
        for (int i = 0; i < pickups.Length; i++)
        {
            if (roomBounds.Contains(pickups[i].transform.position))
            {
                Rigidbody rb = pickups[i].GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    pickups[i].AddComponent<WasNonKinematic>();
                }
            }
        }
    }

    private void OnRoomEnabled(GameObject room)
    {
        WasNonKinematic[] markers = room.GetComponentsInChildren<WasNonKinematic>();
        for (int i = 0; i < markers.Length; i++)
        {
            Rigidbody rb = markers[i].GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = false;
            Destroy(markers[i]);
        }

        Bounds roomBounds = GetRoomBounds(room);
        GameObject[] pickups = GameObject.FindGameObjectsWithTag("Pickup");
        for (int i = 0; i < pickups.Length; i++)
        {
            WasNonKinematic marker = pickups[i].GetComponent<WasNonKinematic>();
            if (marker != null && roomBounds.Contains(pickups[i].transform.position))
            {
                Rigidbody rb = pickups[i].GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = false;
                Destroy(marker);
            }
        }

        NavMeshSurface surface = room.GetComponentInChildren<NavMeshSurface>();
        if (surface != null)
            surface.BuildNavMesh();

        Doors doors = room.GetComponentInChildren<Doors>();
        if (doors != null)
            StartCoroutine(DelayedRefreshDoors(doors));
    }

    private Bounds ComputeRoomBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(room.transform.position, Vector3.one * 20f);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private Bounds GetRoomBounds(GameObject room)
    {
        Bounds b = ComputeRoomBounds(room);
        b.Expand(2f);
        return b;
    }

    private System.Collections.IEnumerator DelayedRefreshDoors(Doors doors)
    {
        yield return null;
        if (doors != null)
            doors.RefreshDoorState();
    }

    private class WasNonKinematic : MonoBehaviour { }
}