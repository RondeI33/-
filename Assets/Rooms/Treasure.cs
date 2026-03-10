using UnityEngine;

public class Treasure : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;

    void Start()
    {
        GameObject spawned = Instantiate(prefabs[Random.Range(0, prefabs.Length)], transform.position, transform.rotation, transform);
    }
}