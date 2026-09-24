using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using UnityEngine.UI;
using Localisation;
using StaTSpace;
using Vector3 = UnityEngine.Vector3;

namespace StaTSpace
{
    /// <summary>
    /// 各メッセージの登録を行うクラス
    /// RegisterIFFMessageType: 各IFFのBlockクラスとsessionIDとの対応を送り、クライアントのIFFDictへの登録を行わせる
    /// PrimaryChangeMessageType: 自身の一次ロック中の敵一覧の更新をホストに伝える
    /// PrimaryChangeMessageType: 自身の二次ロック中の敵一覧の更新をホストに伝える
    /// CurrentAimingMessageType: 自身のロック中のIFFのIDとそのロック状態をホストに伝える（毎フレーム）
    /// CurrentCameraMessageType: ロック状態のIFFが無い時に、自身のカメラの位置と向きをホストに伝える（CurrentAimingMessageが無いフレーム全て）
    /// </summary>
    public static class StaTMessageController
    {
        public static MessageType RegisterIFFMessageType;

        public static MessageType IFFDeadMessageType;

        public static MessageType PrimaryChangeMessageType;

        public static MessageType SecondaryChangeMessageType;

        public static MessageType CurrentAimingMessageType;

        public static MessageType CurrentCameraMessageType;

        /// <summary>
        /// 各メッセージの登録を行う関数
        /// </summary>
        public static void SetUpMessage()
        {
            RegisterIFFMessageType = ModNetworking.CreateMessageType(DataType.Block, DataType.Integer);
            ModNetworking.Callbacks[RegisterIFFMessageType] += new Action<Message>(OnRegisterIFFReceived);

            PrimaryChangeMessageType = ModNetworking.CreateMessageType(DataType.Integer, DataType.IntegerArray);
            ModNetworking.Callbacks[PrimaryChangeMessageType] += new Action<Message>(OnPrimaryChangeReceived);

            SecondaryChangeMessageType = ModNetworking.CreateMessageType(DataType.Integer, DataType.IntegerArray);
            ModNetworking.Callbacks[SecondaryChangeMessageType] += new Action<Message>(OnSecondaryChangeReceived);

            //n番のプレイヤーが、IDがn番のIFFを、n次ロックしている
            CurrentAimingMessageType = ModNetworking.CreateMessageType(DataType.Integer, DataType.Integer, DataType.Integer);
            ModNetworking.Callbacks[CurrentAimingMessageType] += new Action<Message>(OnCurrentAimingReceived);

            //n番のプレイヤーのカメラ方向
            CurrentCameraMessageType = ModNetworking.CreateMessageType(DataType.Integer, DataType.Vector3, DataType.Vector3);
            ModNetworking.Callbacks[CurrentCameraMessageType] += new Action<Message>(OnCurrentCameraReceived);
        }

        /// <summary>
        /// （クライアントのみ）受け取ったIFFのBlockクラスとsessionIDの対応から、IFFを登録する関数を呼ぶ
        /// </summary>
        private static void OnRegisterIFFReceived(Message message)
        {
            //受け取ったデータから、ブロックとSessionIDを取得
            Block block = (Block)message.GetData(0);    //0: Block
            int sessionID = (int)message.GetData(1);    //1: SessionID

            //IFFを登録（SessionID, BlockBehaviour, StaTIFFBlockModuleBehaviour）
            StatTIFFIDRegister.RegisterIFFClient(sessionID, block.InternalObject, block.GameObject.GetComponent<StaTIFFBlockModuleBehaviour>());
        }

        /// <summary>
        /// （ホストのみ）受け取った一次ロック済リストを登録
        /// </summary>
        private static void OnPrimaryChangeReceived(Message message)
        {
            //シミュ状態じゃなければ弾く
            if(!StaTGameStateObserver.IsSimulating)
            {
                return;
            }

            //受け取ったデータから、プレイヤーID（=送信元）と一次ロック済リストを取得
            int PlayerID = (int)message.GetData(0); //0: プレイヤーID
            List<int> PrimaryList = new List<int> ((int[])message.GetData(1));  //1: 一次ロック済リスト

            StaTTargetControllerHost.PrimaryChanged(PlayerID, PrimaryList);
        }

        /// <summary>
        /// （ホストのみ）受け取った二次ロック済リストを登録
        /// </summary>
        private static void OnSecondaryChangeReceived(Message message)
        {
            //シミュ状態じゃなければ弾く
            if (!StaTGameStateObserver.IsSimulating)
            {
                return;
            }

            //受け取ったデータから、プレイヤーID（=送信元）と二次ロック済リストを取得
            int PlayerID = (int)message.GetData(0); //0: プレイヤーID
            List<int> SecondaryList = new List<int>((int[])message.GetData(1)); //1: 一次ロック済リスト

            StaTTargetControllerHost.SecondaryChanged(PlayerID, SecondaryList);
        }

        /// <summary>
        /// （ホストのみ）受け取ったロック中の対象とその状態を登録
        /// </summary>
        private static void OnCurrentAimingReceived(Message message)
        {
            //シミュ状態じゃなければ弾く
            if (!StaTGameStateObserver.IsSimulating)
            {
                return;
            }

            //受け取ったデータから、プレイヤーID（=送信元）とIFFのID、ロック状態を取得
            int playerID = (int)message.GetData(0);
            int sessionID = (int)message.GetData(1);
            StaTLockState lockState = (StaTLockState)message.GetData(2);

            //そのプレイヤーの照準情報を取得、更新する
            var info = StaTTargetControllerHost.PlayerTargetingInfoList[playerID];
            info.LockingSomething = true;   //ロック中かどうかをtrueに
            info.CurrentAimID = sessionID;  //照準中のIFFのID
            info.LockState = lockState;     //何次ロックか
            info.CurrentAimRigidbody = StaTTargetController.IFFDict[sessionID].Rigidbody;   //ロック中のIFFのRigidbody
        }

        /// <summary>
        /// （ホストのみ）受け取ったカメラの位置と向きを登録
        /// </summary>
        private static void OnCurrentCameraReceived(Message message)
        {
            //シミュ状態じゃなければ弾く
            if (!StaTGameStateObserver.IsSimulating)
            {
                return;
            }

            //受け取ったデータから、プレイヤーID（=送信元）とカメラ位置、カメラ向きを取得
            int playerID = (int)message.GetData(0);
            Vector3 camPosition = (Vector3)message.GetData(1);
            Vector3 camForward = (Vector3)message.GetData(2);

            //そのプレイヤーの照準情報を取得、更新する
            var info = StaTTargetControllerHost.PlayerTargetingInfoList[playerID];
            info.LockingSomething = false;  //ロック中かどうかをfalseに
            info.CamPosition = camPosition; //カメラ位置
            info.CamForward = camForward;   //カメラ向き
        }
    }
}
