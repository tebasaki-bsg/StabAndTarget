using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Modding;

namespace StaTSpace
{
    public class StaTGameStateObserver : MonoBehaviour
    {
        public static StaTGameStateObserver Instance { get; private set; }   //シングルトン

        public static bool IsSimulating = false;

        public void Awake()
        {
            Instance = this;
        }

        public void FixedUpdate()
        {
            //各ブロックのSafeAwake()より速い
            if(StatMaster.levelSimulating && !IsSimulating)
            {
                IsSimulating = true;
                Mod.Log("Simulation started");
            }
            else
            if(!StatMaster.levelSimulating && IsSimulating)
            {
                IsSimulating = false;
                Mod.Log("Simulation stopped");

                StatTIFFIDRegister.ClearDictionary();
                StaTTargetController.init = false;

                StatTStabIDContoroller.ClearDictionary();

                StaTSoundController.Instance.StopAllSound();
            }
        }
    }
}
