using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate_Key : MonoBehaviour, ITriggerable
{
    public int keyLockID = 0;

    public void OnTrigger(Player player)
    {
        player.keyIDs.Add( keyLockID);

        GetComponent<ResetObjectOnLevelReset>().DisableObject();
    }

    public void OnTriggerArrive(Player player)
    {
    }

    public void OnTriggerLeave(Player player)
    {
    }
}
