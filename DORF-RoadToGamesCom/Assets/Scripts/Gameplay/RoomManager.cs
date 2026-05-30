using System;
using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static event Action<bool> OnRoomChanged;
    
    public GameObject outside;
    public List<GameObject> indoorRooms; // Hallway, Kitchen, Bathroom

    public GameObject kitchen;
    public GameObject kitchenOnlyObject; // Boden Hallway blocker

    private GameObject currentRoom;

    void Start()
    {
        ActivateRoom(outside);
    }

    public void ActivateRoom(GameObject newRoom)
    {
        if (currentRoom == newRoom) return;

        if (newRoom == outside)
        {
            // DRAUSSEN → alles sichtbar
            outside.SetActive(true);

            foreach (GameObject room in indoorRooms)
                room.SetActive(true);

            kitchenOnlyObject.SetActive(false);
            OnRoomChanged?.Invoke(true);
        }
        else
        {
            // DRINNEN → nur aktueller Raum
            outside.SetActive(false);

            foreach (GameObject room in indoorRooms)
                room.SetActive(room == newRoom);

            // 🔥 Nur wenn Kitchen aktiv ist
            kitchenOnlyObject.SetActive(newRoom == kitchen);
            OnRoomChanged?.Invoke(false);
        }

        currentRoom = newRoom;
    }
}