using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    public RoomManager roomManager;
    public GameObject roomToActivate;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            roomManager.ActivateRoom(roomToActivate);
        }
    }
}