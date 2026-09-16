using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Serialization;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using UnityEngine.UI;
using Localisation;

namespace StaTSpace
{
    //XML上での名前
    [XmlRoot("StaTIFFBlockModule")]
    [Reloadable]

    public class StaTIFFBlockModule : BlockModule
    {
        [XmlElement("IsEnemyToggle")]
        [RequireToValidate]
        public MToggleReference IsEnemyToggle;
    }

    public class StaTIFFBlockModuleBehaviour : BlockModuleBehaviour<StaTIFFBlockModule>
    {
        /*
         * IFFブロックが持つ情報
         *      MyName:     機体名
         *      isEnemy:    敵であるか（ONにすると同じプレイヤー・チームでも照準可能に）
         *      Team:       チーム（自前で取得）
         *      Alive:      生存中か
         *      UseHealth   体力バーを使うか
         *      Health      現在の体力
         * 
         */

        public MToggle IsEnemyToggle;
        public bool isEnemy = false;

        public MText NameText;
        public string myName;

        public int sessionID;
        public int SessionID
        {
            get
            {
                return sessionID;
            }
        }

        public MPTeam Team;
        internal bool alive;
        internal bool useHealth = false;
        internal float health = 0f;

        public string MyName
        {
            get
            {
                return myName;
            }
        }

        public bool Alive
        {
            get
            {
                return alive;
            }
        }
        public bool UseHealth
        {
            get
            {
                return useHealth;
            }
        }
        public float Health     //必ず最大値で割ることで 0≦Health≦1 とすること。
        {
            get
            {
                return health;
            }
        }

        public override void SafeAwake()
        {
            base.SafeAwake();

            //設定欄にエフェクトの名前を追加
            NameText = BlockBehaviour.AddText(Mod.isJapanese ? "機体名" : "Machine Name", "machine-name", "UNKNOWN");
            NameText.DisplayInMapper = true;

            //建築中⇒IFFの辞書をクリア（シミュ開始時に綺麗な状態とするため）
            if(BlockBehaviour.isBuildBlock)
            {
                if(StaTTargetController.init)
                {
                    StatTIFFIDRegister.ClearDictionary();
                    StaTTargetController.init = false;
                }
            }
        }

        ///<summary>
        ///シミュ開始時、ID, ブロックの持ち主のID, BlockBehaviourを揃って登録
        /// </summary>
        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            IsEnemyToggle = GetToggle(Module.IsEnemyToggle);
            isEnemy = IsEnemyToggle.IsActive;

            myName = NameText.Value;

            //自分のチームを取得
            Player Player = Player.From(BlockBehaviour.ParentMachine.PlayerID);
            Team = Player.Team;

            //ホストならID登録を行う
            if (StatMaster.isHosting || !StatMaster.isMP || StatMaster.isLocalSim)
            {
                //ID登録命令
                sessionID = StatTIFFIDRegister.RegisterIFF(BlockBehaviour, this);
            }
            
        }

        public void Dead()
        {
            alive = false;

        }

    }
}
