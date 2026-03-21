using UnityEngine;
using System.Collections.Generic;

public class RoomActivator : MonoBehaviour
{
    private List<GameObject> allRooms = new List<GameObject>();
    private Dictionary<GameObject, List<GameObject>> adjacency = new Dictionary<GameObject, List<GameObject>>();
    private GameObject currentRoom;

    /// <summary>
    /// Called once after generation. adjacencyMap is built by RoomGenerator at placement time —
    /// no distance re-checks, no activate/deactivate side-effects.
    /// </summary>
    public void Init(List<GameObject> rooms,
                     Dictionary<GameObject, List<GameObject>> adjacencyMap)
    {
        allRooms = new List<GameObject>(rooms);
        adjacency = adjacencyMap;
        currentRoom = null;

        // Make sure every room has at least an empty entry so ActivateAround never throws.
        for (int i = 0; i < allRooms.Count; i++)
            if (allRooms[i] != null && !adjacency.ContainsKey(allRooms[i]))
                adjacency[allRooms[i]] = new List<GameObject>();

        // Start with everything off; the first OnPlayerEnteredRoom call will turn the right ones on.
        for (int i = 0; i < allRooms.Count; i++)
            if (allRooms[i] != null)
                allRooms[i].SetActive(false);
    }

    public void OnPlayerEnteredRoom(GameObject room)
    {
        if (room == null || room == currentRoom) return;
        currentRoom = room;
        ActivateAround(room);
    }

    private void ActivateAround(GameObject center)
    {
        // Only the room the player is in + its direct neighbors stay active.
        HashSet<GameObject> shouldBeActive = new HashSet<GameObject>();
        shouldBeActive.Add(center);

        if (adjacency.TryGetValue(center, out List<GameObject> neighbors))
            for (int i = 0; i < neighbors.Count; i++)
                if (neighbors[i] != null)
                    shouldBeActive.Add(neighbors[i]);

        for (int i = 0; i < allRooms.Count; i++)
        {
            if (allRooms[i] == null) continue;
            bool want = shouldBeActive.Contains(allRooms[i]);
            if (allRooms[i].activeSelf != want)
                allRooms[i].SetActive(want);
        }
    }
}