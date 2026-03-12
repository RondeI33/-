using UnityEngine;
using System.Collections.Generic;

public class RoomActivator : MonoBehaviour
{
    private List<GameObject> allRooms = new List<GameObject>();
    private Dictionary<GameObject, List<GameObject>> adjacency = new Dictionary<GameObject, List<GameObject>>();
    private GameObject currentRoom;

    public void Init(List<GameObject> rooms, float snapThreshold)
    {
        allRooms = new List<GameObject>(rooms);
        adjacency.Clear();
        currentRoom = null;

        for (int i = 0; i < allRooms.Count; i++)
            if (allRooms[i] != null)
                adjacency[allRooms[i]] = new List<GameObject>();

        BuildAdjacency(snapThreshold);

        for (int i = 0; i < allRooms.Count; i++)
            if (allRooms[i] != null)
                allRooms[i].SetActive(false);

        if (allRooms.Count > 0 && allRooms[0] != null)
            ActivateAround(allRooms[0]);
    }

    private List<Transform> GetConnectionPoints(GameObject room)
    {
        List<Transform> pts = new List<Transform>();
        Transform root = room.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == "Entrence" || child.name == "Exit")
                pts.Add(child);
        }
        return pts;
    }

    private void BuildAdjacency(float snapThreshold)
    {
        List<(GameObject room, Vector3 pos)> allPts = new List<(GameObject, Vector3)>();

        for (int i = 0; i < allRooms.Count; i++)
        {
            if (allRooms[i] == null) continue;
            bool was = allRooms[i].activeSelf;
            allRooms[i].SetActive(true);
            List<Transform> pts = GetConnectionPoints(allRooms[i]);
            for (int j = 0; j < pts.Count; j++)
                allPts.Add((allRooms[i], pts[j].position));
            allRooms[i].SetActive(was);
        }

        for (int a = 0; a < allPts.Count; a++)
        {
            for (int b = a + 1; b < allPts.Count; b++)
            {
                if (allPts[a].room == allPts[b].room) continue;
                if (Vector3.Distance(allPts[a].pos, allPts[b].pos) > snapThreshold) continue;

                GameObject ra = allPts[a].room;
                GameObject rb = allPts[b].room;

                if (!adjacency.ContainsKey(ra)) adjacency[ra] = new List<GameObject>();
                if (!adjacency.ContainsKey(rb)) adjacency[rb] = new List<GameObject>();

                if (!adjacency[ra].Contains(rb)) adjacency[ra].Add(rb);
                if (!adjacency[rb].Contains(ra)) adjacency[rb].Add(ra);
            }
        }
    }

    public void OnPlayerEnteredRoom(GameObject room)
    {
        if (room == null || room == currentRoom) return;
        currentRoom = room;
        ActivateAround(room);
    }

    private void ActivateAround(GameObject center)
    {
        HashSet<GameObject> shouldBeActive = new HashSet<GameObject>();
        shouldBeActive.Add(center);

        if (adjacency.ContainsKey(center))
        {
            List<GameObject> neighbors = adjacency[center];
            for (int i = 0; i < neighbors.Count; i++)
                if (neighbors[i] != null)
                    shouldBeActive.Add(neighbors[i]);
        }

        for (int i = 0; i < allRooms.Count; i++)
        {
            if (allRooms[i] == null) continue;
            bool want = shouldBeActive.Contains(allRooms[i]);
            if (allRooms[i].activeSelf != want)
                allRooms[i].SetActive(want);
        }
    }
}