using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Modding;
using Modding.Blocks;
using Modding.Common;


namespace StaTSpace
{
    /// <summary>
    /// コアブロックに追加するスクリプト
    /// </summary>
    public class StaTStartingBlockScript : BlockScript
    {
        public MKey TargetChangeKey;

        public Rigidbody rigidbody;
        public BlockBehaviour blockBehaviour;

        public bool localIsOwner = true;

        //シミュ開始時ならば各種情報を登録
        public void Awake()
        {
            blockBehaviour = GetComponent<BlockBehaviour>();
            TargetChangeKey = blockBehaviour.AddKey(Mod.isJapanese? "ターゲット切替え":"Change Target", "target-change", KeyCode.B);

            if(!blockBehaviour.isBuildBlock)
            {

                rigidbody = GetComponent<Rigidbody>();
                localIsOwner = LocalIsOwner();

                //TargetControllerが初期化されていない場合は初期化
                if(!StaTTargetController.init)
                {
                    if(localIsOwner)
                    {
                        Player Player = Player.From(blockBehaviour.ParentMachine.PlayerID);
                        StaTTargetController.MyTeam = Player.Team;
                    }

                    StaTTargetController.SimulationStartInit();
                    StaTTargetControllerHost.SimulationStartInit();
                }
                
            }
        }

        /// <summary>
        /// シミュ中は毎フレームCorePosition, CamForward, CoreSpeedを更新
        /// </summary>
        public void FixedUpdate()
        {
            if(blockBehaviour.isBuildBlock)
            {
                return;
            }

            if(localIsOwner)
            {
                StaTTargetController.CorePosition = transform.position;
                StaTTargetController.CamForward = Camera.main.transform.forward;

                if(TargetChangeKey.IsPressed || TargetChangeKey.EmulationPressed())
                {
                    StaTTargetController.TargetChangePressed = true;
                }
            }
            
        }

        public bool LocalIsOwner()  //プレイヤーのIDとブロックの親のIDを比べる関数
        {
            int BlockPlayerID, OwnerID;

            if(!StatMaster.isMP)
            {
                return true;
            }
            else if(StatMaster.PlayMode == BesiegePlayMode.Spectator)
            {
                return false;
            }
            else
            {
                BlockPlayerID = blockBehaviour.ParentMachine.PlayerID;
                OwnerID = PlayerMachine.GetLocal().Player.NetworkId;

                return BlockPlayerID == OwnerID ? true : false;
            }
        }
    }
}
