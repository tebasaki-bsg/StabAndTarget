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


        //OnLoad()は、バレンやレベルエディタなど、ゲーム起動から最初にゲームモードが決まった瞬間に呼ばれるらしい
        public void Awake()
        {
            Instance = this;

            //最初のコアブロックだけはOnBlockPlaced()が呼ばれないため、イベントで検知して貼り付ける
            Events.OnSceneWithModsCameraInitialised += AddScriptToCore;
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

        /// <summary>
        /// 最初のコアブロックにスクリプトを貼り付ける関数
        /// </summary>
        public void AddScriptToCore()
        {
            GameObject core = GameObject.Find("Building Machine").transform.Find("StartingBlock").gameObject;
            if (core.GetComponent<StaTStartingBlockScript>() == null)
            {
                core.AddComponent<StaTStartingBlockScript>();
            }
        }
    }
}
