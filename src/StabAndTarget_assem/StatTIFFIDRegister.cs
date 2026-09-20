using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Modding;
using Modding.Blocks;

namespace StaTSpace
{
    /// <summary>
    /// IFFの登録・読み出しを行う関数をまとめたクラス。
    /// </summary>
    public static class StatTIFFIDRegister
    {
        private static int NextSessionID = 0;

        /// <summary>
        /// ホスト側でIDを発行し、IFFブロックの登録を行う関数。
        /// ホストがIFFModuleBehaviour.OnSimulateStart から呼ぶ。
        /// </summary>
        public static int RegisterIFF(BlockBehaviour bb, StaTIFFBlockModuleBehaviour iffBehaviour)
        {
            if (bb == null) return 0;   //nullの時はどうでもいいので0を返す

            //登録用に引数からIFFEntryを作る
            var iffEntry = new IFFEntry
            {
                SessionID = NextSessionID,
                IFFName = iffBehaviour.myName,
                BlockBehaviour = bb,
                IFFBehaviour = iffBehaviour,
                Rigidbody = bb.GetComponent<Rigidbody>(),
                IsEnemy = iffBehaviour.isEnemy,
                Team = iffBehaviour.team,
                Alive = iffBehaviour.alive,
                UseHealth = iffBehaviour.useHealth,
                Health = iffBehaviour.health,
                LockState = StaTLockState.None,
                CurrentAiming = false
            };

            //IDとIFFEntryを辞書に登録
            StaTTargetController.IFFDict.Add(NextSessionID, iffEntry);

            //チームごとに敵のIDをまとめた辞書にも登録
            foreach (MPTeam team in Enum.GetValues(typeof(MPTeam)))
            {
                //同じチームかつ敵でない場合はスルー
                if(iffEntry.Team == team && !iffEntry.IsEnemy)
                {
                    continue;
                }

                //それ以外は辞書にIDを登録
                StaTTargetController.IFFTeamListDict[team].Add(NextSessionID);
            }

            //敵勢力でない場合は敵側から見た辞書にも登録
            if (!iffBehaviour.isEnemy)
            {
                StaTTargetController.IFFDictForEnemy.Add(NextSessionID, iffEntry);
            }

            //マルチの時はクライアントに登録命令を送信（ソロのレベルエディタがあるので>1とする）
            if (StatMaster.isMP)
            {
                //クライアントに登録命令を送信
                ModNetworking.SendToAll(StaTMessageController.RegisterIFFMessageType.CreateMessage(Block.From(bb), NextSessionID));
            }
            

            NextSessionID++;

            return NextSessionID - 1;
        }

        /// <summary>
        /// クライアントがホストから受け取った情報からIFFを登録する関数
        /// </summary>
        public static void RegisterIFFClient(int sessionID, BlockBehaviour bb, StaTIFFBlockModuleBehaviour iffBehaviour)
        {
            //登録用に引数からIFFEntryを作る
            var iffEntry = new IFFEntry
            {
                SessionID = sessionID,
                IFFName = iffBehaviour.myName,
                BlockBehaviour = bb,
                IFFBehaviour = iffBehaviour,
                Rigidbody = bb.GetComponent<Rigidbody>(),
                IsEnemy = iffBehaviour.isEnemy,
                Team = iffBehaviour.team,
                Alive = iffBehaviour.alive,
                UseHealth = iffBehaviour.useHealth,
                Health = iffBehaviour.health,
                LockState = StaTLockState.None,
                CurrentAiming = false
            };

            //IDとIFFEntryを辞書に登録
            StaTTargetController.IFFDict.Add(sessionID, iffEntry);

            //チームごとに敵のIDをまとめた辞書にも登録
            foreach (MPTeam team in Enum.GetValues(typeof(MPTeam)))
            {
                //同じチームかつ敵でない場合はスルー
                if (iffEntry.Team == team && !iffEntry.IsEnemy)
                {
                    continue;
                }

                //それ以外は辞書にIDを登録
                StaTTargetController.IFFTeamListDict[team].Add(sessionID);
            }

            //敵勢力でない場合は敵側から見た辞書にも登録
            if (!iffBehaviour.isEnemy)
            {
                StaTTargetController.IFFDictForEnemy.Add(NextSessionID, iffEntry);
            }
        }

        /// <summary>
        /// 辞書を消す関数。
        /// StabBaseModuleBehaviour.OnSimulateStop から呼ぶ。
        /// </summary>
        public static void ClearDictionary()
        {
            //IDとIFFのブロックを繋ぐ辞書をクリア
            StaTTargetController.IFFDict.Clear();

            //チームごとに敵をまとめた辞書もクリア
            foreach (MPTeam team in Enum.GetValues(typeof(MPTeam)))
            {
                StaTTargetController.IFFTeamListDict[team].Clear();
            }

            //敵側から見た辞書もクリア
            StaTTargetController.IFFDictForEnemy.Clear();

            //各ロック済・中リストをクリア
            StaTTargetController.PrimaryLockedList.Clear();
            StaTTargetController.SecondaryLockedList.Clear();
            StaTTargetController.SecondaryLockingTimerDict.Clear();

            //ホストの持つ各プレイヤーのロック情報をまとめた辞書もクリア
            StaTTargetControllerHost.PlayerTargetingInfoList.Clear();

            NextSessionID = 0;
        }

        //被撃破時などに各辞書・リストから自身を消させる
        public static void RemoveMe(int SessionID)
        {
            StaTTargetController.IFFDict.Remove(SessionID);

            //チームごとに敵をまとめた辞書からも削除
            foreach (MPTeam team in Enum.GetValues(typeof(MPTeam)))
            {
                //値が存在するか確認
                if(StaTTargetController.IFFTeamListDict[team].Contains(SessionID))
                {
                    StaTTargetController.IFFTeamListDict[team].Remove(SessionID);
                }
            }

            //敵側から見た辞書からも削除
            StaTTargetController.IFFDictForEnemy.Remove(SessionID);

            //被照準状態かの確認を、各ロック済辞書を消す前に行う
            bool wasCurrentAim = (StaTTargetController.CurrentAimID == SessionID);

            if(StaTTargetController.PrimaryLockedList.Contains(SessionID))
            {
                StaTTargetController.PrimaryLockedList.Remove(SessionID);
            }

            if (StaTTargetController.SecondaryLockedList.Contains(SessionID))
            {
                StaTTargetController.SecondaryLockedList.Remove(SessionID);
            }

            if (StaTTargetController.SecondaryLockingTimerDict.ContainsKey(SessionID))
            {
                StaTTargetController.SecondaryLockingTimerDict.Remove(SessionID);
            }

            if(wasCurrentAim)
            {
                StaTTargetController.Instance.AutoReselect();
            }
        }
    }

    //IFFブロックのID, 機体名, BlockBehaviourをまとめたクラス
    public class IFFEntry
    {
        public int SessionID;

        public string IFFName;

        public BlockBehaviour BlockBehaviour;
        public StaTIFFBlockModuleBehaviour IFFBehaviour;
        public Rigidbody Rigidbody;

        public bool IsEnemy;
        public MPTeam Team;
        public bool Alive;
        public bool UseHealth;
        public float Health;
        public StaTLockState LockState;
        public bool CurrentAiming;

    }
}
