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
    /// </summary>
    public static class StaTMessageController
    {
        public static MessageType RegisterIFFMessageType;

        public static MessageType IFFDeadMessageType;

        public static MessageType PrimaryChangeMessageType;

        public static MessageType SecondaryChangeMessageType;

        public static MessageType CurrentAimingMessageType;

        public static MessageType CurrentCameraMessageType;

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
            CurrentCameraMessageType = ModNetworking.CreateMessageType(DataType.Integer, DataType.Vector3);
            ModNetworking.Callbacks[CurrentCameraMessageType] += new Action<Message>(OnCurrentCameraReceived);
        }


        private static void OnRegisterIFFReceived(Message message)
        {
            Block block = (Block)message.GetData(0);
            int SessionID = (int)message.GetData(1);

            StatTIFFIDRegister.RegisterIFFClient(SessionID, block.InternalObject, block.GameObject.GetComponent<StaTIFFBlockModuleBehaviour>());
        }

        private static void OnPrimaryChangeReceived(Message message)
        {
            int PlayerID = (int)message.GetData(0);
            List<int> PrimaryList = new List<int> ((int[])message.GetData(1));

            StaTTargetControllerHost.PrimaryChanged(PlayerID, PrimaryList);
        }

        private static void OnSecondaryChangeReceived(Message message)
        {
            int PlayerID = (int)message.GetData(0);
            List<int> SecondaryList = new List<int>((int[])message.GetData(1));

            StaTTargetControllerHost.SecondaryChanged(PlayerID, SecondaryList);
        }

        //クライアントからロック中の対象が毎フレーム送られる。何もロックしていない時はカメラ座標が別メッセージで代わりに送られる。
        private static void OnCurrentAimingReceived(Message message)
        {
            int PlayerID = (int)message.GetData(0);
            int SessionID = (int)message.GetData(1);
            StaTLockState LockTier = (StaTLockState)message.GetData(2);
        }

        //クライアントが何もロックしていない時はカメラ座標が送られる。
        private static void OnCurrentCameraReceived(Message message)
        {
            int PlayerID = (int)message.GetData(0);
            Vector3 CameraForward = (Vector3)message.GetData(1);
        }
    }
}
