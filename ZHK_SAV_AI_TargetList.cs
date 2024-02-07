
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ZHK_SAV_AI_TargetList : UdonSharpBehaviour
{
    public SaccAirVehicle[] TargetList;
    public ZHK_SAV_AI[] UsageAI;
    void Start()
    {
            gameObject.SetActive(false);   
    }

    public void UsageAICall(ZHK_SAV_AI x)
    {
        ZHK_SAV_AI[] temp = new ZHK_SAV_AI[UsageAI.Length + 1];
        UsageAI.CopyTo(temp, 0);
        temp[UsageAI.Length] = x;
        UsageAI = temp;
    }
}
