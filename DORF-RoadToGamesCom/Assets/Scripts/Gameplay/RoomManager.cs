using System;
using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static event Action<bool> OnRoomChanged;

    /// <summary>
    /// Wo Marlene gerade ist, true heißt draußen. Für alle, die erst nach dem Wechsel dazukommen
    /// und <see cref="OnRoomChanged"/> deshalb nicht mehr hören konnten.
    /// </summary>
    public static bool IsOutside { get; private set; } = true;

    public GameObject outside;
    public List<GameObject> indoorRooms; // Hallway, Kitchen, Bathroom

    public GameObject kitchen;
    public GameObject kitchenOnlyObject; // Boden Hallway blocker

    private GameObject currentRoom;

    void Awake()
    {
        // IsOutside ist static und überlebt den Szenenwechsel. Scene 2 fängt immer draußen an,
        // also hier zurücksetzen, damit ein zweiter Besuch nicht mit dem alten Wert startet.
        IsOutside = true;
    }

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
            IsOutside = true;
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
            IsOutside = false;
            OnRoomChanged?.Invoke(false);
        }

        currentRoom = newRoom;
    }
}