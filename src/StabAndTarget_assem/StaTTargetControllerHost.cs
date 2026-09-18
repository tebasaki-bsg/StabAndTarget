using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace StaTSpace
{
    /// <summary>
    /// クライアントから送信された一次ロック/二次ロック済リストを反映し保存するクラス
    /// </summary>
    public static class StaTTargetControllerHost
    {
        public static List<PlayerTargetingInfo> PlayerTargetingInfoList;

        /// <summary>
        /// リスト初期化用の関数, シミュ開始時にコアブロックが呼ぶ
        /// </summary>
        public static void SimulationStartInit()
        {
            PlayerTargetingInfoList = new List<PlayerTargetingInfo>();

            for (int i = 0; i < 20; i++)
            {
                PlayerTargetingInfoList.Add(new PlayerTargetingInfo());
            }
        }

        public static void PrimaryChanged(int playerID, List<int> primaryList)
        {
            PlayerTargetingInfoList[playerID].PrimaryLockedList = primaryList;
        }

        public static void SecondaryChanged(int playerID, List<int> secondaryList)
        {
            PlayerTargetingInfoList[playerID].SecondaryLockedList = secondaryList;
        }
    }

    public class PlayerTargetingInfo
    {
        public Vector3 CamForward;

        public bool LockingSomething;
        public int CurrentAimID;

        public List<int> PrimaryLockedList = new List<int>();

        public List<int> SecondaryLockedList = new List<int>();
    }
}
